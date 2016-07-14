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

        #region First Simple Version

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

        #endregion First Simple Version

        public int FindBlobs(Video video)
        {
            List<FileInfo> files = new DirectoryInfo(video.FramesDirectory).GetFiles("*.jpg").OrderBy(f => f.FullName).ToList();
            var maxFrame = new FrameInfos();
            var previousFrame = new FrameInfos {Number = 0, Frame = new Image<Bgr, byte>(files[0].FullName), File = files[0]};
            var previousEmptyFrame = new FrameInfos { Number = 0, Frame = new Image<Bgr, byte>(files[0].FullName), File = files[0] };
            int resultCount = 0;
            int successiveFound = 0;
            bool previousFound = false;
            for (int index = 1; index < files.Count; index++)
            {
                var currentFrame = new FrameInfos { Number = index, Frame = new Image<Bgr, byte>(files[index].FullName), File = files[index] };

                // Get differences
                bool found = false;
                var videoParts = Helper.GetDifferenceVideoParts(video, currentFrame, previousFrame);
                if (videoParts.Count > 0)
                {
                    found = GetMaxFrameInfos(video, videoParts, previousFound, currentFrame, maxFrame);

                    if (found && video.CompareMode == CompareMode.PreviousEmptyFrame)
                    {
                        // We detect changes using previous and current frames
                        // But in order to get the change areas, we use the previous empty frame
                        Logger.WriteLine();
                        Logger.WriteLine(String.Format("Comparing with PreviousEmptyFrame {0}", previousEmptyFrame.Number));
                        videoParts = Helper.GetDifferenceVideoParts(video, currentFrame, previousEmptyFrame);
                        if (videoParts.Any())
                            found = GetMaxFrameInfos(video, videoParts, previousFound, currentFrame, maxFrame);
                        else
                            found = false;
                        Logger.Write("CompareMode.PreviousEmptyFrame : Done.");
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
                    if (successiveFound > 1 && maxFrame.Frame != null)
                    {
                        Logger.WriteLine();
                        Logger.Write("==> MAXFILE:" + maxFrame.File.Name);
                        string destFileName = Path.Combine(video.ResultsDirectory, maxFrame.File.Name);
                        maxFrame.Frame.Save(destFileName);
                        resultCount++;
                    }

                    maxFrame = new FrameInfos();
                    successiveFound = 0;

                    // If comparing with previous empty frame, we set the previous frame here
                    if (video.CompareMode == CompareMode.PreviousEmptyFrame)
                        previousEmptyFrame = currentFrame;
                }

                previousFound = found;
                Logger.WriteLine();

                // If comparing successive frames, we set the previous frame here
                if (video.CompareMode == CompareMode.SuccessiveFrames || video.CompareMode == CompareMode.PreviousEmptyFrame)
                    previousFrame = currentFrame;
            }

            return resultCount;
        }

        private static bool GetMaxFrameInfos(Video video, List<VideoPart> videoParts, bool previousFound, FrameInfos currentFrame, FrameInfos max)
        {
            bool found = false;

            Logger.Write(" [Rectangles:" + videoParts.Count.ToString("0000") + "]");
            // Something was found
            // First : draw rectangles
            Helper.SimplifyParts(ref videoParts, video.RectangleUnionBuffer);
            Logger.Write(" [Simplified Rectangles:" + videoParts.Count.ToString("0000") + "]");
            Image<Bgr, byte> frameWithRectangles = null;
            foreach (var videoPart in videoParts)
            {
                Logger.Write(String.Format(" [Size:{0}/{1}/{2}%]", videoPart.Rectangle.Width.ToString("000"), videoPart.Rectangle.Height.ToString("000"), videoPart.ChangePercentage.ToString("00")));

                bool isBigObject = (videoPart.Rectangle.Width > video.MinGridDistanceForBigObjects || videoPart.Rectangle.Height > video.MinGridDistanceForBigObjects);
                bool isObject = (videoPart.Rectangle.Width > video.MinGridDistanceForObjectIdentification || videoPart.Rectangle.Height > video.MinGridDistanceForObjectIdentification) && videoPart.ChangePercentage > video.ChangePercentageLimit;
                bool isStillAnObject = previousFound && (videoPart.Rectangle.Width > video.MaxGridDistanceForObjectSwitching || videoPart.Rectangle.Height > video.MaxGridDistanceForObjectSwitching) && videoPart.ChangePercentage > video.ChangePercentageLimit;

                bool detected = isBigObject || isObject || isStillAnObject;
                if (detected)
                {
                    // The blog is big enough.
                    // We found something here
                    Logger.Write(" *");
                    found = true;

                    if (frameWithRectangles == null)
                        frameWithRectangles = new Image<Bgr, byte>(currentFrame.Frame.ToBitmap());

                    frameWithRectangles.Draw(videoPart.Rectangle, new Bgr(Color.Red), 2);
                    int area = videoPart.Rectangle.Width*videoPart.Rectangle.Height;
                    if (area > max.Area)
                    {
                        max.Number = currentFrame.Number;
                        max.Area = area;
                        max.Frame = frameWithRectangles;
                        max.File = currentFrame.File;
                    }
                }
            }

            // Second : save frame as image
            if (found && video.IsDebugMode)
            {
                string destFileName = Path.Combine(video.AllDetectedFramesDirectory, currentFrame.File.Name);
                frameWithRectangles.Save(destFileName);
            }

            return found;
        }
    }
}