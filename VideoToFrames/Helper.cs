using System;
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

            video.FramesDirectory = Path.Combine(outputDirectory, $"Frames ({video.NbFramesPerSecondToExport})");
            video.AbsDiffDirectory = Path.Combine(outputDirectory, $"AbsDiff ({video.NbFramesPerSecondToExport})");

            string resultsBaseDirectory = Path.Combine(outputDirectory, $"Results ({video.NbFramesPerSecondToExport}-{(int)video.CompareMode}-{video.ChangeVal.IdentificationChangeValue}-{video.ChangeVal.ShapeChangeValue}-{Consts.GridPatternFactor}-{video.MinGridDistanceForObjectIdentification}-{video.MaxGridDistanceForObjectSwitching}-{video.RectangleUnionBuffer}-{video.ChangePercentageLimit})");
            video.ResultsDirectory = Path.Combine(resultsBaseDirectory, "OK");
            video.AllDetectedFramesDirectory = Path.Combine(resultsBaseDirectory, "AllDetected");
            video.Logfile = Path.Combine(resultsBaseDirectory, "results.log");

            if (!Directory.Exists(video.ResultsDirectory))
                Directory.CreateDirectory(video.ResultsDirectory);
            if (video.IsDebugMode)
            {
                if (!Directory.Exists(video.FramesDirectory))
                    Directory.CreateDirectory(video.FramesDirectory);
                if (!Directory.Exists(video.AbsDiffDirectory))
                    Directory.CreateDirectory(video.AbsDiffDirectory);
                if (!Directory.Exists(video.AllDetectedFramesDirectory))
                    Directory.CreateDirectory(video.AllDetectedFramesDirectory);
            }
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

        public static Image<Bgr, byte> GetAbsDiff(FrameInfos currentFrame, FrameInfos previousFrame, string outDirectory, bool debugMode)
        {
            // Calculate AbsDiff
            Image<Bgr, byte> difference = currentFrame.Frame.AbsDiff(previousFrame.Frame);

            if (debugMode)
            {
                // Save AbsDiff
                string destFileName = Path.Combine(outDirectory, currentFrame.File.Name);
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
                var fileName = Path.GetFileNameWithoutExtension(currentFrame.File.Name) + ".txt";
                destFileName = Path.Combine(outDirectory, fileName);
                using (var sw = new StreamWriter(destFileName))
                {
                    sw.Write(str.ToString());
                    sw.Close();
                }
            }

            return difference;
        }

        public static void SimplifyParts(ref List<VideoPart> videoParts, int rectangleUnionBuffer)
        {
            if (videoParts.Count <= 0)
                throw new NotSupportedException();

            var simplifiedParts = new List<VideoPart>();
            simplifiedParts.Add(videoParts.First());

            for (int i = 1; i < videoParts.Count; i++)
            {
                var r1 = videoParts[i].Rectangle;
                bool merged = false;
                for (int j = 0; j < simplifiedParts.Count; ++j)
                {
                    var r2 = videoParts[j].Rectangle;
                    var bufferedR2 = new Rectangle(r2.Left - rectangleUnionBuffer, r2.Top - rectangleUnionBuffer, r2.Width + rectangleUnionBuffer * 2, r2.Height + rectangleUnionBuffer * 2);
                    if (r1.IntersectsWith(bufferedR2))
                    {
                        Rectangle rectangleBlog;
                        Polygon polygonlob;
                        double changePercentage;
                        BlobHelper.MergeBlobs(simplifiedParts[j].Polygon, videoParts[i].Polygon, out rectangleBlog, out polygonlob, out changePercentage);
                        simplifiedParts[j].Rectangle = rectangleBlog;
                        simplifiedParts[j].Polygon = polygonlob;
                        simplifiedParts[j].ChangePercentage = changePercentage;

                        merged = true;
                    }
                }

                if (!merged)
                {
                    simplifiedParts.Add(videoParts[i]);
                }
            }

            if (videoParts.Count != simplifiedParts.Count)
            {
                // A simplification was made.
                // We try to resimplify the list
                SimplifyParts(ref simplifiedParts, rectangleUnionBuffer);
            }

            videoParts = simplifiedParts;
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

        public static List<Point> IdentifyBigChangesCoordinates(Image<Bgr, byte> difference, Rectangle analyseArea, ChangeDetection changeVal)
        {
            var changesCoordinates = new List<Point>();
            for (int i = analyseArea.Top; i < analyseArea.Bottom; i += Consts.GridPatternFactor)
            {
                for (int j = analyseArea.Left; j < analyseArea.Right; j += Consts.GridPatternFactor)
                {
                    var cell = difference[i, j];
                    if (cell.Blue + cell.Green + cell.Red > changeVal.IdentificationChangeValue)
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

        public static List<VideoPart> GetDifferenceVideoParts(Video video, FrameInfos currentFrame, FrameInfos previousFrame)
        {
            var difference = GetAbsDiff(currentFrame, previousFrame, video.AbsDiffDirectory, video.IsDebugMode);

            // Go through every 10 pixels, and test if there is a big change.
            // Remember the coordinates if a change is found
            List<Point> changesCoordinates = IdentifyBigChangesCoordinates(difference, video.AnalyseArea, video.ChangeVal);
            Logger.Write(currentFrame.File.Name);
            Logger.Write(" [ChangeCoords:" + changesCoordinates.Count.ToString("0000") + "]");

            int minMinX, minMinY, maxMaxX, maxMaxY;
            minMinY = minMinX = int.MaxValue;
            maxMaxX = maxMaxY = 0;
            var videoParts = new List<VideoPart>();
            foreach (var coordinate in changesCoordinates)
            {
                if (minMinX < coordinate.X && coordinate.X < maxMaxX &&
                    minMinY < coordinate.Y && coordinate.Y < maxMaxY)
                {
                    // This coordinate is already in a found blob
                    continue;
                }
                // For each coordinate, we try to build the blob of changes
                Rectangle rectangleBlob;
                Polygon polygonBlob;
                double changePercentage;
                BlobHelper.GetBlob(coordinate, difference, video.ChangeVal, out rectangleBlob, out polygonBlob, out changePercentage);
                var newVideoPart = new VideoPart {Rectangle = rectangleBlob, ChangePercentage = changePercentage};
                newVideoPart.Polygon = polygonBlob;
                videoParts.Add(newVideoPart);


                UpdateXyLimits(rectangleBlob.X, rectangleBlob.Y, ref minMinX, ref minMinY, ref maxMaxX, ref maxMaxY);
                UpdateXyLimits(rectangleBlob.X + rectangleBlob.Width, rectangleBlob.Y + rectangleBlob.Height, ref minMinX, ref minMinY, ref maxMaxX, ref maxMaxY);
            }
            return videoParts;
        }
    }
}