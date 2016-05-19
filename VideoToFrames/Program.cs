using System;
using System.Diagnostics;
using System.IO;

namespace VideoToFrames
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var tool = new VideoToFrames();
            //string videoFilename = @"D:\Projects\VideoAnalyse\K23 Verkehrsüberwachung 20.04.2016 12.00-13.00.mp4";
            //string videoFilename = @"D:\Projects\VideoAnalyse\K23 Verkehrsüberwachung 20.04.2016 11.00-12.00.mp4";
            string videoFilename = @"D:\Projects\VideoAnalyse\K23 Verkehrsüberwachung 20.04.2016 17.00-18.00.mp4";
            var startDate = new DateTime(2016, 4, 20, 17, 0, 1);

            string resultsDirectory, framesDirectory, unsureDirectory, logfile;
            GetOutputDirectories(videoFilename, out framesDirectory, out resultsDirectory, out unsureDirectory, out logfile);

            try
            {
                Extract(tool, videoFilename, framesDirectory, startDate);
                IdentifyVehicles(tool, framesDirectory, resultsDirectory, unsureDirectory);
            }
            finally
            {
                Logger.CreateLogFile(logfile);
            }
        }

        private static void GetOutputDirectories(string videoFilename, out string framesDirectory, out string resultsDirectory, out string unsureDirectory, out string logfile)
        {
            string outputDirectory = Path.Combine(Path.GetDirectoryName(videoFilename), Path.GetFileNameWithoutExtension(videoFilename));
            
            framesDirectory = Path.Combine(outputDirectory, "Frames");
            var resultsBaseDirectory = Path.Combine(outputDirectory, "Results");
            resultsDirectory = Path.Combine(resultsBaseDirectory, "OK");
            unsureDirectory = Path.Combine(resultsBaseDirectory, "Unsure");
            logfile = Path.Combine(resultsBaseDirectory, "results.log");

            if (!Directory.Exists(framesDirectory))
                Directory.CreateDirectory(framesDirectory);
            if (!Directory.Exists(resultsDirectory))
                Directory.CreateDirectory(resultsDirectory);
            if (!Directory.Exists(unsureDirectory))
                Directory.CreateDirectory(unsureDirectory);
        }

        private static void IdentifyVehicles(VideoToFrames tool, string framesDirectory, string resultsDirectory, string unsureDirectory)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            tool.FindVehicles(framesDirectory, resultsDirectory, unsureDirectory);
            stopwatch.Stop();
            Logger.WriteLine();
            Logger.WriteLine("Identification executed in " + stopwatch.Elapsed.ToString("c"));
        }

        private static void Extract(VideoToFrames tool, string videoFilename, string framesDirectory, DateTime startDate)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            tool.Extract(videoFilename, startDate, framesDirectory);
            stopwatch.Stop();
            Logger.WriteLine();
            Logger.WriteLine("Extraction executed in " + stopwatch.Elapsed.ToString("c"));
        }
    }
}