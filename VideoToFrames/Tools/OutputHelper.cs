using System;
using System.Globalization;
using System.IO;
using VideoToFrames.Analyse;
using VideoToFrames.Basis;

namespace VideoToFrames.Tools
{
    public class OutputHelper
    {
        public static void GetOutputDirectories(Video video)
        {
            string outputDirectory = Path.Combine(Path.GetDirectoryName(video.VideoFilename), Path.GetFileNameWithoutExtension(video.VideoFilename));

            video.FramesDirectory = Path.Combine(outputDirectory, $"Frames ({video.NbFramesPerSecondToExport})");
            video.AbsDiffDirectory = Path.Combine(outputDirectory, $"AbsDiff ({video.NbFramesPerSecondToExport})");

            string resultsBaseDirectory = Path.Combine(outputDirectory, $"Results ([{video.NbFramesPerSecondToExport}.{(int) video.CompareMode}]-[{video.ChangeContext}]-[{Consts.GridPatternFactor}.{video.MinGridDistanceForObjectIdentification}.{video.MaxGridDistanceForObjectSwitching}.{video.RectangleUnionBuffer}])");
            video.ResultsDirectory = Path.Combine(resultsBaseDirectory, "OK");
            video.ResultsDirectoryB = Path.Combine(video.ResultsDirectory, "B");
            video.ResultsDirectoryT = Path.Combine(video.ResultsDirectory, "T");
            video.AllDetectedFramesDirectory = Path.Combine(resultsBaseDirectory, "AllDetected");
            video.Logfile = Path.Combine(resultsBaseDirectory, "results.log");

            if (!Directory.Exists(video.ResultsDirectory))
            {
                Directory.CreateDirectory(video.ResultsDirectory);
                Directory.CreateDirectory(video.ResultsDirectoryB);
                Directory.CreateDirectory(video.ResultsDirectoryT);
            }

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
            int milliseconds = (int) Math.Floor(i*3600000d/frameCount);
            var span = new TimeSpan(0, 0, 0, 0, milliseconds);
            var time = startDate.Add(span);

            string filename = String.Format("{0} ({1}).{2}"
                , i.ToString(CultureInfo.InvariantCulture).PadLeft(pad, '0')
                , time.ToString("yyyy-MM-dd HH.mm.ss.") + time.Millisecond.ToString(CultureInfo.InvariantCulture).PadLeft(3, '0')
                , "jpg");
            string outputFilename = Path.Combine(outputDirectory, filename);
            return outputFilename;
        }
    }
}