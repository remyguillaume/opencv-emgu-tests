using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AForge.Video.FFMPEG;
using Emgu.CV;
using Emgu.CV.Structure;

namespace VideoToFrames
{
    public class VideoToFrames
    {
        public Rectangle GetVideoSize(string sourceVideoFileName)
        {
            var reader = new VideoFileReader();
            reader.Open(sourceVideoFileName);

            try
            {
                Bitmap bitmap = reader.ReadVideoFrame();
                return new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        /// Extracts frames from video
        /// </summary>
        /// <param name="sourceVideoFileName"></param>
        /// <param name="startDate"></param>
        /// <param name="outputDirectory"></param>
        public void Extract(string sourceVideoFileName, DateTime startDate, string outputDirectory)
        {
            var reader = new VideoFileReader();
            reader.Open(sourceVideoFileName);

            try
            {
                // We do not need to export every frame, this is too much
                // reader.FrameRate gives the nmber of frames per second
                // reader.FrameCount is the total number of frames in the video.
                int frameInterval = reader.FrameRate / Consts.NbFramesPerSecondToExport;
                var pad = reader.FrameCount.ToString(CultureInfo.InvariantCulture).Length;
                for (int i = 0; i < reader.FrameCount; ++i)
                {
                    Logger.Write(i + " ");
                    Bitmap bitmap = reader.ReadVideoFrame();

                    if (i % 1000 == 0)
                        Logger.WriteLine(); 
                    if (i % frameInterval != 0)
                        continue;

                    var outputFilename = GetOutputFilename(outputDirectory, i, reader.FrameCount, pad, startDate);
                    using (var memory = new MemoryStream())
                    {
                        using (var fs = new FileStream(outputFilename, FileMode.Create, FileAccess.ReadWrite))
                        {
                            bitmap.Save(memory, ImageFormat.Jpeg);
                            byte[] bytes = memory.ToArray();
                            fs.Write(bytes, 0, bytes.Length);
                        }
                    }
                }
            }
            finally
            {
                reader.Close();
            }
        }

        private static string GetOutputFilename(string outputDirectory, int i, double frameCount, int pad, DateTime startDate)
        {
            int milliseconds = (int)Math.Floor(i*3600000d/frameCount);
            var span = new TimeSpan(0, 0, 0, 0, milliseconds);
            var time = startDate.Add(span);

            string filename = String.Format("{0} ({1}).{2}"
                , i.ToString(CultureInfo.InvariantCulture).PadLeft(pad, '0')
                , time.ToString("yyyy-MM-dd HH.mm.ss.") + time.Millisecond.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0')
                , "jpg");
            string outputFilename = Path.Combine(outputDirectory, filename);
            return outputFilename;
        }

        /// <summary>
        /// First version of algorithm to find vehicles.
        /// This version is a simple algorithm, and do not find all data
        /// Please use the new version with blogs-finding instead
        /// </summary>
        /// <param name="framesDirectory"></param>
        /// <param name="resultsDirectory"></param>
        /// <param name="unsureDirectory"></param>
        [Obsolete]
        public void FindVehiclesSimpleVersion(string framesDirectory, string resultsDirectory, string unsureDirectory)
        {
            FileInfo maxFile = null;
            int maxValue = 0;
            int nbSuccessiveChanges = 0;
            bool identificationInProgress = false;

            List<FileInfo> files = new DirectoryInfo(framesDirectory).GetFiles("*.jpg").OrderBy(f => f.FullName).ToList();
            var previousFrame = new Image<Bgr, byte>(files[0].FullName);
            for (int i = 1; i < files.Count; i++)
            {
                FileInfo currentFile = files[i];
                var frame = new Image<Bgr, byte>(currentFile.FullName);
                var difference = frame.AbsDiff(previousFrame);
                var nbChanges = GetNumberOfPixelWithBigChange(difference);
                Logger.WriteLine(String.Format("{0} : [{1}]", currentFile.Name, nbChanges));
                previousFrame = frame;

                if (!identificationInProgress && nbChanges > Consts.MinChangeValueToDetectVehicle)
                {
                    // Big change
                    // Something is happening here
                    identificationInProgress = true;
                }
                if (identificationInProgress)
                {
                    if (nbChanges < Consts.MaxChangeValueToDetectEndOfVehicle)
                    {
                        // end of identification
                        if (nbSuccessiveChanges >= 5)
                        {
                            // This is something, almost sure
                            string filename = String.Format("{0} ({1}-{2}){3}", Path.GetFileNameWithoutExtension(maxFile.Name), nbSuccessiveChanges, maxValue, Path.GetExtension(maxFile.Name));
                            string destFileName = Path.Combine(resultsDirectory, filename);
                            File.Copy(maxFile.FullName, destFileName);
                        }
                        else if (nbSuccessiveChanges >= 2)
                        {
                            // Not sure here
                            string filename = String.Format("{0} ({1}-{2}){3}", Path.GetFileNameWithoutExtension(maxFile.Name), nbSuccessiveChanges, maxValue, Path.GetExtension(maxFile.Name));
                            string destFileName = Path.Combine(unsureDirectory, filename);
                            File.Copy(maxFile.FullName, destFileName);
                        }
                        else
                        {
                            // 1 >= nbSuccessiveChanges >= 2
                            // This is very probably nothing, but just some noise in the picture, or camera move
                            // Nothing to do
                        }
                        identificationInProgress = false;
                        maxValue = 0;
                        maxFile = null;
                        nbSuccessiveChanges = 0;
                    }
                    else
                    {
                        nbSuccessiveChanges += 1;
                        if (nbChanges > maxValue)
                        {
                            maxValue = nbChanges;
                            maxFile = currentFile;
                        }
                    }
                }
            }
        }

        private int GetNumberOfPixelWithBigChange(Image<Bgr, byte> difference)
        {
            // Try just 1 pixel of 4 to increase performance
            int count = 0;
            for (int i = 0; i < difference.Height; i += Consts.PerformanceImprovmentFactor)
            {
                for (int j = 0; j < difference.Width; j += Consts.PerformanceImprovmentFactor)
                {
                    var cell = difference[i, j];
                    if (cell.Blue + cell.Green + cell.Red > Consts.BigChangeVal)
                        count += Consts.PerformanceImprovmentFactor * Consts.PerformanceImprovmentFactor;
                }
            }

            return count;
        }

        public int FindBlobs(string framesDirectory, string absDiffDirectory, string resultsDirectory, string unsureDirectory, Rectangle analyseArea)
        {
            List<FileInfo> files = new DirectoryInfo(framesDirectory).GetFiles("*.jpg").OrderBy(f => f.FullName).ToList();
            var previousFrame = new Image<Bgr, byte>(files[0].FullName);
            int maxArea = 0;
            Image<Bgr, byte> maxFrame = null;
            FileInfo maxFile = null;
            int resultCount = 0;
            int successiveFound = 0;
            for (int index = 1; index < files.Count; index++)
            {
                FileInfo currentFile = files[index];
                var frame = new Image<Bgr, byte>(currentFile.FullName);
                var difference = GetAbsDiff(frame, previousFrame, absDiffDirectory, currentFile);

                // Go through every 10 pixels, and test if there is a big change.
                // Remember the coordinates if a change is found
                List<Point> changesCoordinates = IdentifyBigChangesCoordinates(difference, analyseArea);
                Logger.Write(currentFile.Name);
                Logger.Write(" [ChangeCoords:"+changesCoordinates.Count.ToString("0000")+"]");

                int minMinX, minMinY, maxMaxX, maxMaxY;
                minMinY = minMinX = int.MaxValue;
                maxMaxX = maxMaxY = 0;
                var rectangles = new List<Rectangle>();
                foreach (var coordinate in changesCoordinates)
                {
                    if (minMinX < coordinate.X && coordinate.X < maxMaxX &&
                        minMinY < coordinate.Y && coordinate.Y < maxMaxY)
                    {
                        // This coordinate is already in a found blob
                        continue;
                    }
                    // For each coordiante, we try to build the blog of changes
                    int minX, minY, maxX, maxY;
                    GetMinAndMaxXyValuesVersion2(coordinate, difference, out minX, out minY, out maxX, out maxY);
                    var rectangle = new Rectangle(minX, minY, maxX - minX, maxY - minY);
                    rectangles.Add(rectangle);

                    UpdateXyLimits(minX, minY, ref minMinX, ref minMinY, ref maxMaxX, ref maxMaxY);
                    UpdateXyLimits(maxX, maxY, ref minMinX, ref minMinY, ref maxMaxX, ref maxMaxY);
                }

                bool found = false;
                if (rectangles.Count > 0)
                {
                    Logger.Write(" [Rectangles:" + rectangles.Count.ToString("0000") + "]");
                    // Something was found
                    // First : draw rectangles
                    SimplifyRectangles(ref rectangles);
                    Logger.Write(" [Simplified Rectangles:" + rectangles.Count.ToString("0000") + "]");
                    foreach (var rectangle in rectangles)
                    {
                        Logger.Write(String.Format(" [Size:{0}/{1}]", rectangle.Width.ToString("000"), rectangle.Height.ToString("000")));
                        if (rectangle.Width > Consts.MaxGridDistanceForObjectIdentification || rectangle.Height > Consts.MaxGridDistanceForObjectIdentification)
                        {
                            // The blog is big enough.
                            // We found something here
                            Logger.Write(" *");
                            found = true;
                            frame.Draw(rectangle, new Bgr(Color.Red), 2);
                            int area = rectangle.Width*rectangle.Height;
                            if (area > maxArea)
                            {
                                maxArea = area;
                                maxFrame = frame;
                                maxFile = currentFile;
                            }
                        }
                    }

                    // Second : save frame as image
                    if (found)
                    {
                        string destFileName = Path.Combine(unsureDirectory, currentFile.Name);
                        frame.Save(destFileName);
                    }
                }

                if (found)
                {
                    // Something was found
                    // If the are not minimum 2 consecituve frames with whanges, we consider it as "no-change" at all
                    successiveFound++;
                }
                else
                {
                    // No change found in this frame.
                    // It means we switch vehicle.
                    // We can save maxFrame as a definitive result
                    if (successiveFound > 1 && maxFrame != null)
                    {
                        Logger.WriteLine();
                        Logger.Write("==> MAXFILE:" + maxFile.Name);
                        string destFileName = Path.Combine(resultsDirectory, maxFile.Name);
                        maxFrame.Save(destFileName);
                        resultCount++;
                    }

                    maxArea = 0;
                    maxFrame = null;
                    maxFile = null;
                    successiveFound = 0;
                }

                Logger.WriteLine();
            }

            return resultCount;
        }

        private void SimplifyRectangles(ref List<Rectangle> rectangles)
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
                    var bufferedR2 = new Rectangle(r2.Left - Consts.RectangleUnionBuffer, r2.Top - Consts.RectangleUnionBuffer, r2.Width + Consts.RectangleUnionBuffer*2, r2.Height + Consts.RectangleUnionBuffer*2);
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

        private static void UpdateXyLimits(int x, int y, ref int minX, ref int minY, ref int maxX, ref int maxY)
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

        private static Image<Bgr, byte> GetAbsDiff(Image<Bgr, byte> frame, Image<Bgr, byte> previousFrame, string outDirectory, FileInfo fileInfo)
        {
            // Calculate AbsDiff
            Image<Bgr, byte> difference = frame.AbsDiff(previousFrame);

            if (Consts.Debug)
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

        private static void GetMinAndMaxXyValues(Point coordinate, Image<Bgr, byte> difference, out int minX, out int minY, out int maxX, out int maxY)
        {
            // First : find minX
            int x = coordinate.X;
            int y = coordinate.Y;
            var cell = difference[y, x];
            double cellValue = cell.Blue + cell.Green + cell.Red;
            while (x > 0 && cellValue > Consts.BigChangeVal)
            {
                x -= 1;
                cell = difference[y, x];
                cellValue = cell.Blue + cell.Green + cell.Red;
            }
            // We found a first value for minX (but perhaps not exactly the right value yet). 
            minX = x;

            // Second : find maxX
            x = coordinate.X;
            y = coordinate.Y;
            cell = difference[y, x];
            cellValue = cell.Blue + cell.Green + cell.Red;
            while (x < difference.Width && cellValue > Consts.BigChangeVal)
            {
                x += 1;
                cell = difference[y, x];
                cellValue = cell.Blue + cell.Green + cell.Red;
            }
            // We found a first value for maxX (but perhaps not exactly the right value yet). 
            maxX = x;

            // Third : find minY
            x = coordinate.X;
            y = coordinate.Y;
            cell = difference[y, x];
            cellValue = cell.Blue + cell.Green + cell.Red;
            while (y > 0 && cellValue > Consts.BigChangeVal)
            {
                y -= 1;
                cell = difference[y, x];
                cellValue = cell.Blue + cell.Green + cell.Red;
            }
            // We found a first value for minY (but perhaps not exactly the right value yet). 
            minY = y;

            // Fourth : find maxY
            x = coordinate.X;
            y = coordinate.Y;
            cell = difference[y, x];
            cellValue = cell.Blue + cell.Green + cell.Red;
            while (y < difference.Height && cellValue > Consts.BigChangeVal)
            {
                y += 1;
                cell = difference[y, x];
                cellValue = cell.Blue + cell.Green + cell.Red;
            }
            // We found a first value for maxY (but perhaps not exactly the right value yet). 
            maxY = y;
        }

        private static void GetMinAndMaxXyValuesVersion2(Point coordinate, Image<Bgr, byte> difference, out int minX, out int minY, out int maxX, out int maxY)
        {
            // We try to build the outline or the object
            // Method : Up, Right, Down, Left
            var alreadyTested = new bool[difference.Height, difference.Width];
            minY = minX = int.MaxValue;
            maxX = maxY = 0;
            GetMinAndMaxXyValuesRecursive(difference, coordinate.X, coordinate.Y, ref minX, ref minY, ref maxX, ref maxY, alreadyTested, 1);
        }

        private static void GetMinAndMaxXyValuesRecursive(Image<Bgr, byte> frame, int x, int y, ref int minX, ref int minY, ref int maxX, ref int maxY, bool[,] alreadyTested, int callStackLevel)
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
                if (cellValue > Consts.BigChangeVal)
                {
                    // Ok, this cell can be included in the blob
                    UpdateXyLimits(x, y, ref minX, ref minY, ref maxX, ref maxY);

                    // If this cell is still in the blob, search recursively for linked cells.
                    GetMinAndMaxXyValuesRecursive(frame, x, y - 1, ref minX, ref minY, ref maxX, ref maxY, alreadyTested, callStackLevel + 1);
                    GetMinAndMaxXyValuesRecursive(frame, x + 1, y, ref minX, ref minY, ref maxX, ref maxY, alreadyTested, callStackLevel + 1);
                    GetMinAndMaxXyValuesRecursive(frame, x, y + 1, ref minX, ref minY, ref maxX, ref maxY, alreadyTested, callStackLevel + 1);
                    GetMinAndMaxXyValuesRecursive(frame, x - 1, y, ref minX, ref minY, ref maxX, ref maxY, alreadyTested, callStackLevel + 1);
                }
            }
        }

        private static List<Point> IdentifyBigChangesCoordinates(Image<Bgr, byte> difference, Rectangle analyseArea)
        {
            var changesCoordinates = new List<Point>();
            for (int i = analyseArea.Top; i < analyseArea.Bottom; i += Consts.GridPatternFactor)
            {
                for (int j = analyseArea.Left; j < analyseArea.Right; j += Consts.GridPatternFactor)
                {
                    var cell = difference[i, j];
                    if (cell.Blue + cell.Green + cell.Red > Consts.BigChangeVal)
                    {
                        // Change found
                        changesCoordinates.Add(new Point(j, i));
                    }
                }
            }
            return changesCoordinates;
        }
    }
}