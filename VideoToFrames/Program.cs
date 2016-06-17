using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace VideoToFrames
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var videos = new List<Video>();
            videos.AddRange(new[] {
                /*new Video
                {
                    VideoFilename = @"D:\Projects\VideoAnalyse\K23 Verkehrsüberwachung 20.04.2016 17.00-18.00.mp4",
                    StartDate = new DateTime(2016, 4, 20, 17, 0, 1),
                    LimitLeft = 360,
                    LimitTop = 80,
                    LimitBottom = 420,
                    ChangeVal = 150
                },
                new Video
                {
                    VideoFilename = @"D:\Projects\VideoAnalyse\K23 Verkehrsüberwachung 20.04.2016 6.00-7.00.mp4",
                    StartDate = new DateTime(2016, 4, 20, 6, 0, 1),
                    LimitLeft = 360,
                    LimitTop = 80,
                    LimitBottom = 420
                },                
                new Video
                {
                    VideoFilename = @"D:\Projects\VideoAnalyse\K23 Verkehrsüberwachung 20.04.2016 11.00-12.00.mp4",
                    StartDate = new DateTime(2016, 4, 20, 11, 0, 1),
                    LimitLeft = 360,
                    LimitTop = 80,
                    LimitBottom = 420
                },    */            
                new Video
                {
                    VideoFilename = @"D:\Projects\VideoAnalyse\K23 Verkehrsüberwachung 20.04.2016 12.00-13.00.mp4",
                    StartDate = new DateTime(2016, 4, 20, 12, 0, 1),
                    LimitLeft = 360,
                    LimitTop = 80,
                    LimitBottom = 420,
                    ChangeVal = 350,
                    NbFramesPerSecondToExport = 4,
                    MaxGridDistanceForObjectIdentification = 70
                },
            });

            foreach (Video video in videos)
            {
                Helper.GetOutputDirectories(video);
                var tool = new VideoAnalyser();
                try
                {
                    video.VideoSize = tool.GetVideoSize(video);
                    Extract(tool, video);
                    //IdentifyVehiclesVersion1(tool, framesDirectory, resultsDirectory, unsureDirectory);
                    IdentifyVehicles(tool, video);
                }
                finally
                {
                    Logger.CreateLogFile(video.Logfile);
                    Logger.Reset();
                }
            }
        }

        /*
        private static void IdentifyVehiclesVersion1(VideoAnalyser tool, string framesDirectory, string resultsDirectory, string unsureDirectory)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            tool.FindVehiclesSimpleVersion(framesDirectory, resultsDirectory, unsureDirectory);
            stopwatch.Stop();
            Logger.WriteLine();
            Logger.WriteLine("Identification executed in " + stopwatch.Elapsed.ToString("c"));
        }
        */

        private static void Extract(VideoAnalyser tool, Video video)
        {
            if (Directory.GetFiles(video.FramesDirectory).Any())
            {
                // Files already extracted for this configuration.
                // Nothing to do
                Logger.WriteLine("Skipping extraction (already executed for this configuration)");
                return;
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            tool.Extract(video);
            stopwatch.Stop();
            Logger.WriteLine();
            Logger.WriteLine("Extraction executed in " + stopwatch.Elapsed.ToString("c"));
        }

        private static void IdentifyVehicles(VideoAnalyser tool, Video video)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // If there are perturbations (other route which should not be detected)
            // We will analyse only the right area
            video.AnalyseArea = Helper.GetAnalyseArea(video);

            int nbResults = tool.FindBlobs(video);
            stopwatch.Stop();
            Logger.WriteLine();
            Logger.WriteLine("Identification executed in " + stopwatch.Elapsed.ToString("c"));
            Logger.WriteLine("Number of vehicle found : " + nbResults);
        }
    }
}