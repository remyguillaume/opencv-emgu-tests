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
            List<Video> videos = GetVideosList(@"J:\Videos_UR\");
            //List<Video> videos = GetVideosList(@"J:\Videos_UR\Aufnahmen vom 19. - 25.04.2016\Standard_SCU5N2_2016-04-19_0500", "Standard_SCU5N2_2016-04-19_0500.011.mp4");

            foreach (Video video in videos)
            {
                Helper.GetOutputDirectories(video);
                var tool = new VideoAnalyser();
                bool executed = false;
                try
                {
                    video.VideoSize = tool.GetVideoSize(video);

                    if (video.IsDebugMode)
                    {
                        Extract(tool, video);
                        executed = IdentifyVehicles(tool, video);
                    }
                    else
                    {
                        executed = IdentifyVehiclesWithoutExtraction(tool, video);
                    }
                }
                catch
                {
                    executed = true;
                }
                finally
                {
                    if (executed)
                        Logger.CreateLogFile(video.Logfile);

                    Logger.Reset();
                }
            }
        }

        private static List<Video> GetVideosList(string path, string pattern = null)
        {
            var videos = new List<Video>();
            string searchPattern = pattern ?? "*.mp4";
            foreach (string file in Directory.GetFiles(path, searchPattern))
            {
                DateTime startDateTime = GetStartDateTimeFromFileName(Path.GetFileNameWithoutExtension(file));

                videos.Add(
                    new Video
                    {
                        VideoFilename = file,
                        StartDate = startDateTime,
                        LimitLeft = 360,
                        LimitTop = 80,
                        LimitBottom = 420,
                        LimitRight = 710,
                        ChangeVal = GetChangeVal(file),
                        CompareMode = GetCompareMode(file)
                    });
            }

            // Recursively load video in subfolders
            foreach (string directory in Directory.GetDirectories(path))
            {
                videos.AddRange(GetVideosList(directory, pattern));
            }

            return videos;
        }

        private static CompareMode GetCompareMode(string file)
        {
            var compareMode = CompareMode.PreviousEmptyFrame;
            return compareMode;            
        }

        private static int GetChangeVal(string file)
        {
            int changeVal = 250;

            //// Particular cases : To many false positive
            //if (file.Contains("Standard_SCU5N2_2016-04-19_0500.011"))
            //    changeVal = 350;

            return changeVal;
        }

        private static DateTime GetStartDateTimeFromFileName(string filename)
        {
            string[] splittedFilename = filename.Split('_');
            string date = splittedFilename[2];
            string time = splittedFilename[3];

            string[] splittedDate = date.Split('-');
            int year = Convert.ToInt32(splittedDate[0].Trim());
            int month = Convert.ToInt32(splittedDate[1].Trim());
            int day = Convert.ToInt32(splittedDate[2].Trim());

            string[] splittedTime = time.Split('.');
            int t1 = Convert.ToInt32(splittedTime[0].Trim('0', ' '));
            int t2 = Convert.ToInt32(splittedTime[1].Trim());
            int hour = t1 + t2 - 1;
            int minute = 0;
            int second = 1;

            return new DateTime(year, month, day, hour, minute, second);
        }

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

        private static bool IdentifyVehicles(VideoAnalyser tool, Video video)
        {
            if (Directory.GetFiles(video.ResultsDirectory).Any())
            {
                // Analyse was already made for this configuration.
                // Nothing to do
                Logger.WriteLine("Skipping analyse (already executed for this configuration)");
                return false;
            }

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

            return true;
        }

        private static bool IdentifyVehiclesWithoutExtraction(VideoAnalyser tool, Video video)
        {
            if (Directory.GetFiles(video.ResultsDirectory).Any())
            {
                // Analyse was already made for this configuration.
                // Nothing to do
                Logger.WriteLine("Skipping analyse (already executed for this configuration)");
                return false;
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // If there are perturbations (other route which should not be detected)
            // We will analyse only the right area
            video.AnalyseArea = Helper.GetAnalyseArea(video);

            int nbResults = tool.FindBlobsWithoutExtraction(video);
            stopwatch.Stop();
            Logger.WriteLine();
            Logger.WriteLine("Identification executed in " + stopwatch.Elapsed.ToString("c"));
            Logger.WriteLine("Number of vehicle found : " + nbResults);

            return true;
        }
    }
}