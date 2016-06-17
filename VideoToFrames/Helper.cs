﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Emgu.CV;
using Emgu.CV.Structure;

namespace VideoToFrames
{
    public class Helper
    {
        public static void GetOutputDirectories(Video video)
        {
            string outputDirectory = Path.Combine(Path.GetDirectoryName(video.VideoFilename), Path.GetFileNameWithoutExtension(video.VideoFilename));

            video.FramesDirectory = Path.Combine(outputDirectory, String.Format("Frames ({0})", video.NbFramesPerSecondToExport));
            video.AbsDiffDirectory = Path.Combine(outputDirectory, String.Format("AbsDiff ({0})", video.NbFramesPerSecondToExport));
            //string resultsBaseDirectory = Path.Combine(outputDirectory, String.Format("Results ({0}-{1}-{2}-{3})", Consts.BigChangeVal, Consts.MaxChangeValueToDetectEndOfVehicle, Consts.MinChangeValueToDetectVehicle, Consts.PerformanceImprovmentFactor));
            string resultsBaseDirectory = Path.Combine(outputDirectory, String.Format("Results ({0}-{1}-{2}-{3}-{4})", video.NbFramesPerSecondToExport, video.ChangeVal, Consts.GridPatternFactor, video.MaxGridDistanceForObjectIdentification, Consts.RectangleUnionBuffer));
            video.ResultsDirectory = Path.Combine(resultsBaseDirectory, "OK");
            video.AllDetectedFramesDirectory = Path.Combine(resultsBaseDirectory, "AllDetected");
            video.Logfile = Path.Combine(resultsBaseDirectory, "results.log");

            if (!Directory.Exists(video.FramesDirectory))
                Directory.CreateDirectory(video.FramesDirectory);
            if (!Directory.Exists(video.AbsDiffDirectory))
                Directory.CreateDirectory(video.AbsDiffDirectory);
            if (!Directory.Exists(video.ResultsDirectory))
                Directory.CreateDirectory(video.ResultsDirectory);
            if (!Directory.Exists(video.AllDetectedFramesDirectory))
                Directory.CreateDirectory(video.AllDetectedFramesDirectory);
        }

        public static string GetOutputFilename(string outputDirectory, int i, double frameCount, int pad, DateTime startDate)
        {
            int milliseconds = (int)Math.Floor(i * 3600000d / frameCount);
            var span = new TimeSpan(0, 0, 0, 0, milliseconds);
            var time = startDate.Add(span);

            string filename = String.Format("{0} ({1}).{2}"
                , i.ToString(CultureInfo.InvariantCulture).PadLeft(pad, '0')
                , time.ToString("yyyy-MM-dd HH.mm.ss.") + time.Millisecond.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0')
                , "jpg");
            string outputFilename = Path.Combine(outputDirectory, filename);
            return outputFilename;
        }

        public static Image<Bgr, byte> GetAbsDiff(Image<Bgr, byte> frame, Image<Bgr, byte> previousFrame, string outDirectory, FileInfo fileInfo, bool debugMode)
        {
            // Calculate AbsDiff
            Image<Bgr, byte> difference = frame.AbsDiff(previousFrame);

            if (debugMode)
            {
                // Save AbsDiff
                string destFileName = Path.Combine(outDirectory, fileInfo.Name);
                difference.Save(destFileName);

                // Save AbsDiff as text values
                var str = new StringBuilder("\t");
                for (int j = 0; j < difference.Width; ++j)
                    str.Append(j).Append("\t");
                str.AppendLine();

                for (int i = 0; i < difference.Height; i += 1)
                {
                    str.Append(i).Append("\t");
                    for (int j = 0; j < difference.Width; j += 1)
                    {
                        var cell = difference[i, j];
                        var cellValue = cell.Blue + cell.Green + cell.Red;
                        str.Append(cellValue).Append("\t");
                    }
                    str.AppendLine();
                }
                var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name) + ".txt";
                destFileName = Path.Combine(outDirectory, fileName);
                using (var sw = new StreamWriter(destFileName))
                {
                    sw.Write(str.ToString());
                    sw.Close();
                }
            }

            return difference;
        }

        public static void SimplifyRectangles(ref List<Rectangle> rectangles)
        {
            if (rectangles.Count <= 0)
                throw new NotSupportedException();

            var simplifiedRectangles = new List<Rectangle>();
            simplifiedRectangles.Add(rectangles.First());

            for (int i = 1; i < rectangles.Count; i++)
            {
                var r1 = rectangles[i];
                bool merged = false;
                for (int j = 0; j < simplifiedRectangles.Count; ++j)
                {
                    var r2 = rectangles[j];
                    var bufferedR2 = new Rectangle(r2.Left - Consts.RectangleUnionBuffer, r2.Top - Consts.RectangleUnionBuffer, r2.Width + Consts.RectangleUnionBuffer * 2, r2.Height + Consts.RectangleUnionBuffer * 2);
                    if (r1.IntersectsWith(bufferedR2))
                    {
                        int minX = r2.Left;
                        int minY = r2.Top;
                        int maxX = r2.Right;
                        int maxY = r2.Bottom;
                        UpdateXyLimits(r1.Left, r1.Top, ref minX, ref minY, ref maxX, ref maxY);
                        UpdateXyLimits(r1.Right, r1.Bottom, ref minX, ref minY, ref maxX, ref maxY);

                        simplifiedRectangles[j] = new Rectangle(minX, minY, maxX - minX, maxY - minY);
                        merged = true;
                    }
                }

                if (!merged)
                {
                    simplifiedRectangles.Add(r1);
                }
            }

            if (rectangles.Count != simplifiedRectangles.Count)
            {
                // A simplification was made.
                // We try to resimplify the list
                SimplifyRectangles(ref simplifiedRectangles);
            }

            rectangles = simplifiedRectangles;
        }

        public static void UpdateXyLimits(int x, int y, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            if (x < minX)
                minX = x;
            if (y < minY)
                minY = y;
            if (x > maxX)
                maxX = x;
            if (y > maxY)
                maxY = y;
        }

        public static void GetMinAndMaxXyValues(Point coordinate, Image<Bgr, byte> difference, out int minX, out int minY, out int maxX, out int maxY, int changeVal)
        {
            // We try to build the outline or the object
            // Method : Up, Right, Down, Left
            var alreadyTested = new bool[difference.Height, difference.Width];
            minY = minX = int.MaxValue;
            maxX = maxY = 0;
            GetMinAndMaxXyValuesRecursive(difference, coordinate.X, coordinate.Y, ref minX, ref minY, ref maxX, ref maxY, alreadyTested, changeVal, 1);
        }

        private static void GetMinAndMaxXyValuesRecursive(Image<Bgr, byte> frame, int x, int y, ref int minX, ref int minY, ref int maxX, ref int maxY, bool[,] alreadyTested, int changeVal, int callStackLevel)
        {
            if (callStackLevel > Math.Max(frame.Width, frame.Height))
                return; // Prevents StackOverflow, because this cell will probably be reached by some other way

            if (x < 0 || y < 0 || x >= frame.Width || y >= frame.Height)
                return;

            if (!alreadyTested[y, x])
            {
                var cell = frame[y, x];
                alreadyTested[y, x] = true;
                double cellValue = cell.Blue + cell.Green + cell.Red;
                if (cellValue > changeVal)
                {
                    // Ok, this cell can be included in the blob
                    UpdateXyLimits(x, y, ref minX, ref minY, ref maxX, ref maxY);

                    // If this cell is still in the blob, search recursively for linked cells.
                    GetMinAndMaxXyValuesRecursive(frame, x, y - 1, ref minX, ref minY, ref maxX, ref maxY, alreadyTested, changeVal, callStackLevel + 1);
                    GetMinAndMaxXyValuesRecursive(frame, x + 1, y, ref minX, ref minY, ref maxX, ref maxY, alreadyTested, changeVal, callStackLevel + 1);
                    GetMinAndMaxXyValuesRecursive(frame, x, y + 1, ref minX, ref minY, ref maxX, ref maxY, alreadyTested, changeVal, callStackLevel + 1);
                    GetMinAndMaxXyValuesRecursive(frame, x - 1, y, ref minX, ref minY, ref maxX, ref maxY, alreadyTested, changeVal, callStackLevel + 1);
                }
            }
        }

        public static List<Point> IdentifyBigChangesCoordinates(Image<Bgr, byte> difference, Rectangle analyseArea, int changeVal)
        {
            var changesCoordinates = new List<Point>();
            for (int i = analyseArea.Top; i < analyseArea.Bottom; i += Consts.GridPatternFactor)
            {
                for (int j = analyseArea.Left; j < analyseArea.Right; j += Consts.GridPatternFactor)
                {
                    var cell = difference[i, j];
                    if (cell.Blue + cell.Green + cell.Red > changeVal)
                    {
                        // Change found
                        changesCoordinates.Add(new Point(j, i));
                    }
                }
            }
            return changesCoordinates;
        }

        public static Rectangle GetAnalyseArea(Video video)
        {
            int minX = (video.LimitLeft > 0) ? video.LimitLeft : 0;
            int minY = (video.LimitTop > 0) ? video.LimitTop : 0;
            int maxX = (video.LimitRight > 0) ? video.LimitRight : video.VideoSize.Right;
            int maxY = (video.LimitBottom > 0) ? video.LimitBottom : video.VideoSize.Bottom;

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }
    }
}