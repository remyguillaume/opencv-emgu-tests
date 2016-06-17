using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using AForge.Video.FFMPEG;
using Emgu.CV;
using Emgu.CV.Structure;

namespace VideoToFrames
{
    public class VideoAnalyser
    {
        /// <summary>
        /// Return the video size
        /// </summary>
        /// <returns></returns>
        public Rectangle GetVideoSize(Video video)
        {
            var reader = new VideoFileReader();
            reader.Open(video.VideoFilename);

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
        /// <param name="video"></param>
        public void Extract(Video video)
        {
            var reader = new VideoFileReader();
            reader.Open(video.VideoFilename);

            try
            {
                // We do not need to export every frame, this is too much
                // reader.FrameRate gives the nmber of frames per second
                // reader.FrameCount is the total number of frames in the video.
                int frameInterval = reader.FrameRate / video.NbFramesPerSecondToExport;
                var pad = reader.FrameCount.ToString(CultureInfo.InvariantCulture).Length;
                for (int i = 0; i < reader.FrameCount; ++i)
                {
                    Logger.Write(i + " ");
                    Bitmap bitmap = reader.ReadVideoFrame();

                    if (i % 1000 == 0)
                        Logger.WriteLine(); 
                    if (i % frameInterval != 0)
                        continue;

                    var outputFilename = Helper.GetOutputFilename(video.FramesDirectory, i, reader.FrameCount, pad, video.StartDate);
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

        //public void FindVehiclesSimpleVersion(string framesDirectory, string resultsDirectory, string unsureDirectory)
        //{
        //    FileInfo maxFile = null;
        //    int maxValue = 0;
        //    int nbSuccessiveChanges = 0;
        //    bool identificationInProgress = false;

        //    List<FileInfo> files = new DirectoryInfo(framesDirectory).GetFiles("*.jpg").OrderBy(f => f.FullName).ToList();
        //    var previousFrame = new Image<Bgr, byte>(files[0].FullName);
        //    for (int i = 1; i < files.Count; i++)
        //    {
        //        FileInfo currentFile = files[i];
        //        var frame = new Image<Bgr, byte>(currentFile.FullName);
        //        var difference = frame.AbsDiff(previousFrame);
        //        var nbChanges = GetNumberOfPixelWithBigChange(difference);
        //        Logger.WriteLine(String.Format("{0} : [{1}]", currentFile.Name, nbChanges));
        //        previousFrame = frame;

        //        if (!identificationInProgress && nbChanges > Consts.MinChangeValueToDetectVehicle)
        //        {
        //            // Big change
        //            // Something is happening here
        //            identificationInProgress = true;
        //        }
        //        if (identificationInProgress)
        //        {
        //            if (nbChanges < Consts.MaxChangeValueToDetectEndOfVehicle)
        //            {
        //                // end of identification
        //                if (nbSuccessiveChanges >= 5)
        //                {
        //                    // This is something, almost sure
        //                    string filename = String.Format("{0} ({1}-{2}){3}", Path.GetFileNameWithoutExtension(maxFile.Name), nbSuccessiveChanges, maxValue, Path.GetExtension(maxFile.Name));
        //                    string destFileName = Path.Combine(resultsDirectory, filename);
        //                    File.Copy(maxFile.FullName, destFileName);
        //                }
        //                else if (nbSuccessiveChanges >= 2)
        //                {
        //                    // Not sure here
        //                    string filename = String.Format("{0} ({1}-{2}){3}", Path.GetFileNameWithoutExtension(maxFile.Name), nbSuccessiveChanges, maxValue, Path.GetExtension(maxFile.Name));
        //                    string destFileName = Path.Combine(unsureDirectory, filename);
        //                    File.Copy(maxFile.FullName, destFileName);
        //                }
        //                else
        //                {
        //                    // 1 >= nbSuccessiveChanges >= 2
        //                    // This is very probably nothing, but just some noise in the picture, or camera move
        //                    // Nothing to do
        //                }
        //                identificationInProgress = false;
        //                maxValue = 0;
        //                maxFile = null;
        //                nbSuccessiveChanges = 0;
        //            }
        //            else
        //            {
        //                nbSuccessiveChanges += 1;
        //                if (nbChanges > maxValue)
        //                {
        //                    maxValue = nbChanges;
        //                    maxFile = currentFile;
        //                }
        //            }
        //        }
        //    }
        //}

        //private int GetNumberOfPixelWithBigChange(Image<Bgr, byte> difference)
        //{
        //    // Try just 1 pixel of 4 to increase performance
        //    int count = 0;
        //    for (int i = 0; i < difference.Height; i += Consts.PerformanceImprovmentFactor)
        //    {
        //        for (int j = 0; j < difference.Width; j += Consts.PerformanceImprovmentFactor)
        //        {
        //            var cell = difference[i, j];
        //            if (cell.Blue + cell.Green + cell.Red > Consts.BigChangeVal)
        //                count += Consts.PerformanceImprovmentFactor * Consts.PerformanceImprovmentFactor;
        //        }
        //    }

        //    return count;
        //}

        public int FindBlobs(Video video)
        {
            List<FileInfo> files = new DirectoryInfo(video.FramesDirectory).GetFiles("*.jpg").OrderBy(f => f.FullName).ToList();
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
                var difference = Helper.GetAbsDiff(frame, previousFrame, video.AbsDiffDirectory, currentFile, video.DebugMode);

                // Go through every 10 pixels, and test if there is a big change.
                // Remember the coordinates if a change is found
                List<Point> changesCoordinates = Helper.IdentifyBigChangesCoordinates(difference, video.AnalyseArea, video.ChangeVal);
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
                    Helper.GetMinAndMaxXyValues(coordinate, difference, out minX, out minY, out maxX, out maxY, video.ChangeVal);
                    var rectangle = new Rectangle(minX, minY, maxX - minX, maxY - minY);
                    rectangles.Add(rectangle);

                    Helper.UpdateXyLimits(minX, minY, ref minMinX, ref minMinY, ref maxMaxX, ref maxMaxY);
                    Helper.UpdateXyLimits(maxX, maxY, ref minMinX, ref minMinY, ref maxMaxX, ref maxMaxY);
                }

                bool found = false;
                if (rectangles.Count > 0)
                {
                    Logger.Write(" [Rectangles:" + rectangles.Count.ToString("0000") + "]");
                    // Something was found
                    // First : draw rectangles
                    Helper.SimplifyRectangles(ref rectangles);
                    Logger.Write(" [Simplified Rectangles:" + rectangles.Count.ToString("0000") + "]");
                    foreach (var rectangle in rectangles)
                    {
                        Logger.Write(String.Format(" [Size:{0}/{1}]", rectangle.Width.ToString("000"), rectangle.Height.ToString("000")));
                        if (rectangle.Width > video.MaxGridDistanceForObjectIdentification || rectangle.Height > video.MaxGridDistanceForObjectIdentification)
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
                        string destFileName = Path.Combine(video.AllDetectedFramesDirectory, currentFile.Name);
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
                        string destFileName = Path.Combine(video.ResultsDirectory, maxFile.Name);
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
    }
}