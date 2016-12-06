using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime;
using AForge.Video.FFMPEG;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using VideoToFrames.Basis;
using VideoToFrames.Tools;

namespace VideoToFrames.Analyse
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
                // reader.FrameRate gives the number of frames per second
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

                    var outputFilename = OutputHelper.GetOutputFilename(video.FramesDirectory, i, reader.FrameCount, pad, video.StartDate);
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
        
        public int FindBlobs(Video video)
        {
            var reader = new VideoFileReader();
            reader.Open(video.VideoFilename);

            try
            {
                // We do not need to export every frame, this is too much
                // reader.FrameRate gives the number of frames per second
                // reader.FrameCount is the total number of frames in the video.
                int frameInterval = reader.FrameRate/video.NbFramesPerSecondToExport;
                var pad = reader.FrameCount.ToString(CultureInfo.InvariantCulture).Length;

                // Read first frame
                FrameInfos previousFrame;
                FrameInfos previousEmptyFrame;
                using (Bitmap bitmap = reader.ReadVideoFrame())
                {
                    string outputFilename = OutputHelper.GetOutputFilename(video.FramesDirectory, 0, reader.FrameCount, pad, video.StartDate);
                    previousFrame = new FrameInfos {Number = 0, Frame = new Image<Bgr, byte>(bitmap), File = new FileInfo(outputFilename)};
                    previousEmptyFrame = new FrameInfos {Number = 0, Frame = new Image<Bgr, byte>(bitmap), File = new FileInfo(outputFilename)};
                }
                var maxFrame = new FrameInfos();
                int resultCount = 0;
                int successiveFound = 0;
                bool previousFound = false;
                var prevMinAndMax = new MinAndMax();
                var directionCounter = new Dictionary<Direction, int>();
                directionCounter[Direction.B] = 0;
                directionCounter[Direction.T] = 0;

                for (int i = 1; i < reader.FrameCount; ++i)
                {
                    FrameInfos currentFrame;
                    using (Bitmap bitmap = reader.ReadVideoFrame())
                    {
                        if (i % 1000 == 0)
                            Logger.WriteLine();
                        if (i % frameInterval != 0)
                            continue;

                        string outputFilename = OutputHelper.GetOutputFilename(video.FramesDirectory, i, reader.FrameCount, pad, video.StartDate);
                        currentFrame = new FrameInfos { Number = i, Frame = new Image<Bgr, byte>(bitmap), File = new FileInfo(outputFilename) };
                    }

                    // Get differences
                    bool found = false;
                    List<VideoPart> videoParts;
                    switch (video.CompareMode)
                    {
                        case CompareMode.SuccessiveFrames:
                            videoParts = Helper.GetDifferenceVideoParts(video, currentFrame, previousFrame);
                            if (videoParts.Count > 0)
                            {
                                // We detect changes using previous and current frames
                                // But in order to get the change areas, we use the previous empty frame
                                Logger.WriteLine();
                                Logger.WriteLine(String.Format("Comparing with PreviousEmptyFrame {0}", previousEmptyFrame.Number));
                                videoParts = Helper.GetDifferenceVideoParts(video, currentFrame, previousEmptyFrame);
                                if (videoParts.Count > 0)
                                    found = GetMaxFrameInfos(video, videoParts, previousFound, currentFrame, maxFrame);
                                Logger.Write(" Done.");
                            }
                            break;
                        case CompareMode.PreviousEmptyFrame:
                            videoParts = Helper.GetDifferenceVideoParts(video, currentFrame, previousEmptyFrame);
                            if (videoParts.Count > 0)
                                found = GetMaxFrameInfos(video, videoParts, previousFound, currentFrame, maxFrame);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    
                    if (found)
                    {
                        // Something was found
                        Rectangle rect = videoParts.First(p => p.ChangeValue == videoParts.Max(v => v.ChangeValue)).Rectangle;
                        if (successiveFound == 0)
                        {
                            prevMinAndMax.Min = rect.X;
                            prevMinAndMax.Max = rect.X + rect.Width;
                        }
                        else
                        {
                            // X Left
                            if (rect.X - prevMinAndMax.Min > 0)
                                directionCounter[Direction.T]++;
                            else
                                directionCounter[Direction.B]++;

                            // X Right
                            if (rect.X + rect.Width - prevMinAndMax.Max > 0)
                                directionCounter[Direction.T]++;
                            else
                                directionCounter[Direction.B]++;

                            prevMinAndMax = new MinAndMax() {Min = rect.X, Max = rect.X + rect.Width};
                        }

                        // If the are not minimum 2 consecutive frames with whanges, we consider it as "no-change" at all
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
                            string filename = video.Export⁬WithChangeValue ? $"{maxFrame.ChangeValue:000000} - {maxFrame.File.Name}" : maxFrame.File.Name;

                            if (!prevMinAndMax.IsValid)
                                throw new NotSupportedException();
                            string outputDirectory = (directionCounter[Direction.B] > directionCounter[Direction.T]) ? video.ResultsDirectoryB : video.ResultsDirectoryT;

                            string destFileName = Path.Combine(outputDirectory, filename);
                            maxFrame.Frame.Save(destFileName);
                            resultCount++;
                        }
                        
                        maxFrame = new FrameInfos();
                        successiveFound = 0;
                        directionCounter[Direction.B] = 0;
                        directionCounter[Direction.T] = 0;
                        prevMinAndMax = new MinAndMax();
                        // We set the previous empty frame here
                        previousEmptyFrame = currentFrame;
                    }

                    previousFound = found;
                    Logger.WriteLine();

                    // We set the previous frame here
                    previousFrame = currentFrame;
                }

                return resultCount;
            }

            finally
            {
                reader.Close();
            }
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
                Logger.Write(String.Format(" [Size:{0}/{1}/{2}%]", videoPart.Rectangle.Width.ToString("000"), videoPart.Rectangle.Height.ToString("000"), videoPart.ChangeValue.ToString("0000")));

                if (IsDetected(video, previousFound, videoPart))
                {
                    // The blob is big enough.
                    // We found something here
                    Logger.Write("*");
                    found = true;

                    if (frameWithRectangles == null)
                        frameWithRectangles = new Image<Bgr, byte>(currentFrame.Frame.ToBitmap());

                    frameWithRectangles.Draw(videoPart.Rectangle, new Bgr(Color.Red), 2);
                    frameWithRectangles.DrawPolyline(videoPart.Polygon.ToArray(), true, new Bgr(Color.Orange), 1, LineType.AntiAlias);

                    int area = videoPart.Rectangle.Width*videoPart.Rectangle.Height;
                    if (area > max.Area)
                    {
                        max.Number = currentFrame.Number;
                        max.Area = area;
                        max.Frame = frameWithRectangles;
                        max.File = currentFrame.File;
                        max.ChangeValue = videoPart.ChangeValue;
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

        private static bool IsDetected(Video video, bool previousFound, VideoPart videoPart)
        {
            bool widthIdentify = videoPart.Rectangle.Width > video.MinGridDistanceForObjectIdentification && videoPart.Rectangle.Height > video.MinGridDistanceForObjectIdentification/2;
            bool heightIdentify = videoPart.Rectangle.Height > video.MinGridDistanceForObjectIdentification && videoPart.Rectangle.Width > video.MinGridDistanceForObjectIdentification/2;
            bool widthSwitch = videoPart.Rectangle.Width > video.MaxGridDistanceForObjectSwitching;
            bool heightSwitch = videoPart.Rectangle.Height > video.MaxGridDistanceForObjectSwitching;
            bool widthBig = videoPart.Rectangle.Width > video.MinGridDistanceForBigObjects;
            bool heightBig = videoPart.Rectangle.Height > video.MinGridDistanceForBigObjects;
            bool isChange = videoPart.ChangeValue > video.ChangeContext.ChangeLimit;

            bool isBigObject = widthBig || heightBig;
            bool isObject = (widthIdentify || heightIdentify) && isChange;
            bool isStillAnObject = previousFound && (widthSwitch || heightSwitch) && isChange;

            bool detected = /*isBigObject || */isObject || isStillAnObject;
            return detected;
        }
    }
}