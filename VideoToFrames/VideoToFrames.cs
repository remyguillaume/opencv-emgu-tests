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
    public class VideoToFrames
    {
        private const int BigChangeVal = 600; // Default value : 600
        private const int PerformanceImprovmentFactor = 2; // NOTE : 1 is no improvment at all

        public void Extract(string sourceVideoFileName, DateTime startDate, string outputDirectory)
        {
            var reader = new VideoFileReader();
            reader.Open(sourceVideoFileName);

            try
            {
                // We do not need to export every frame.
                // reader.FrameRate gives the nmber of frames per second
                // reader.FrameCount is the total number of frames in the video.
                // We will export 4 frames / second, this should be enough
                int frameInterval = reader.FrameRate/4;
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

        public void FindVehicles(string framesDirectory, string resultsDirectory, string unsureDirectory)
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

                if (!identificationInProgress && nbChanges > 50)
                {
                    // Big change
                    // Something is happening here
                    identificationInProgress = true;
                }
                if (identificationInProgress)
                {
                    if (nbChanges < 10)
                    {
                        // end of identification
                        if (nbSuccessiveChanges >= 5)
                        {
                            // This is something, almost sure
                            string filename = String.Format("{0} ({1}-{2}){3}", Path.GetFileNameWithoutExtension(maxFile.Name), nbSuccessiveChanges, maxValue, Path.GetExtension(maxFile.Name));
                            string destFileName = Path.Combine(resultsDirectory, filename);
                            File.Copy(maxFile.FullName, destFileName);
                        }
                        else if (nbSuccessiveChanges >= 3)
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
            for (int i = 0; i < difference.Height; i+=PerformanceImprovmentFactor)
            {
                for (int j = 0; j < difference.Width; j += PerformanceImprovmentFactor)
                {
                    var cell = difference[i, j];
                    if (cell.Blue + cell.Green + cell.Red > BigChangeVal)
                        count+=PerformanceImprovmentFactor*PerformanceImprovmentFactor;
                }
            }

            return count;
        }
    }
}