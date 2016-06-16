using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace VideoToFrames
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var tool = new VideoToFrames();
            string videoFilename = @"D:\Projects\VideoAnalyse\K23 Verkehrsüberwachung 20.04.2016 17.00-18.00.mp4";
            var startDate = new DateTime(2016, 4, 20, 17, 0, 1);

            string resultsDirectory, absDiffDirectory, framesDirectory, unsureDirectory, logfile;
            GetOutputDirectories(videoFilename, out framesDirectory, out absDiffDirectory, out resultsDirectory, out unsureDirectory, out logfile);

            try
            {
                Extract(tool, videoFilename, framesDirectory, startDate);
                //IdentifyVehiclesVersion1(tool, framesDirectory, resultsDirectory, unsureDirectory);
                IdentifyVehiclesVersion2(tool, framesDirectory, absDiffDirectory, resultsDirectory, unsureDirectory);
            }
            finally
            {
                Logger.CreateLogFile(logfile);
            }
        }

        private static void GetOutputDirectories(string videoFilename, out string framesDirectory, out string absDiffDirectory, out string resultsDirectory, out string unsureDirectory, out string logfile)
        {
            string outputDirectory = Path.Combine(Path.GetDirectoryName(videoFilename), Path.GetFileNameWithoutExtension(videoFilename));
            
            framesDirectory = Path.Combine(outputDirectory, String.Format("Frames ({0})", Consts.NbFramesPerSecondToExport));
            absDiffDirectory = Path.Combine(outputDirectory, String.Format("AbsDiff ({0})", Consts.NbFramesPerSecondToExport));
            var resultsBaseDirectory = Path.Combine(outputDirectory, String.Format("Results (V2-{0}-{1}-{2}-{3})", Consts.BigChangeVal, Consts.MaxChangeValueToDetectEndOfVehicle, Consts.MinChangeValueToDetectVehicle, Consts.PerformanceImprovmentFactor));
            resultsDirectory = Path.Combine(resultsBaseDirectory, "OK");
            unsureDirectory = Path.Combine(resultsBaseDirectory, "Unsure");
            logfile = Path.Combine(resultsBaseDirectory, "results.log");

            if (!Directory.Exists(framesDirectory))
                Directory.CreateDirectory(framesDirectory);
            if (!Directory.Exists(absDiffDirectory))
                Directory.CreateDirectory(absDiffDirectory); 
            if (!Directory.Exists(resultsDirectory))
                Directory.CreateDirectory(resultsDirectory);
            if (!Directory.Exists(unsureDirectory))
                Directory.CreateDirectory(unsureDirectory);
        }

        private static void IdentifyVehiclesVersion1(VideoToFrames tool, string framesDirectory, string resultsDirectory, string unsureDirectory)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            tool.FindVehiclesSimpleVersion(framesDirectory, resultsDirectory, unsureDirectory);
            stopwatch.Stop();
            Logger.WriteLine();
            Logger.WriteLine("Identification executed in " + stopwatch.Elapsed.ToString("c"));
        }

        private static void IdentifyVehiclesVersion2(VideoToFrames tool, string framesDirectory, string absDiffDirectory, string resultsDirectory, string unsureDirectory)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            int nbResults = tool.FindBlobs(framesDirectory, absDiffDirectory, resultsDirectory, unsureDirectory);
            stopwatch.Stop();
            Logger.WriteLine();
            Logger.WriteLine("Identification executed in " + stopwatch.Elapsed.ToString("c"));
            Logger.WriteLine("Number of vehicle found : " + nbResults);
        }

        private static void Extract(VideoToFrames tool, string videoFilename, string framesDirectory, DateTime startDate)
        {
            if (Directory.GetFiles(framesDirectory).Any())
            {
                // Files already extracted for this configuration.
                // Nothing to do
                Logger.WriteLine("Skipping extraction (already executed for this configuration)");
                return;
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            tool.Extract(videoFilename, startDate, framesDirectory);
            stopwatch.Stop();
            Logger.WriteLine();
            Logger.WriteLine("Extraction executed in " + stopwatch.Elapsed.ToString("c"));
        }
    }
}