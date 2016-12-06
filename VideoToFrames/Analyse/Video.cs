using System;
using System.Drawing;
using VideoToFrames.Tools;

namespace VideoToFrames.Analyse
{
    public class Video
    {
        public Video()
        {
            LimitLeft = LimitRight = LimitTop = LimitBottom = -1;
            IsDebugMode = false;
            NbFramesPerSecondToExport = 4;
            MinGridDistanceForBigObjects = 150;
            MinGridDistanceForObjectIdentification = 70;
            MaxGridDistanceForObjectSwitching = 30;
            RectangleUnionBuffer = 10;
            CompareMode = CompareMode.SuccessiveFrames;
        }

        public string VideoFilename { get; set; }
        public DateTime StartDate { get; set; }

        /// <summary>
        /// If DebugMode is enabled, details analyse files will be generated (AbsDif pictures, Changes matrixes)
        /// </summary>
        public bool IsDebugMode { get; set; }

        /// <summary>
        /// Indicates the minimum Pixel Value necessary to consider a pixel as a change in the difference frame (AbsDiff)
        /// </summary>
        public ChangeContext ChangeContext { get; set; }

        /// <summary>
        /// When exporting video, number of frames to export per second
        /// </summary>
        public int NbFramesPerSecondToExport { get; set; }

        /// <summary>
        /// Distance in pixels that indicates that an object is big enough to consider it as an object without testing ChangePercentage
        /// </summary>
        public int MinGridDistanceForBigObjects { get; set; }

        /// <summary>
        /// Distance in pixels that indicates that an object is big enough to consider it as an object, 
        /// but ChangePercentage will be necessar to validate this consideration
        /// </summary>
        public int MinGridDistanceForObjectIdentification { get; set; }

        /// <summary>
        /// When going through Frames, if an object is detected on a previous frame
        /// This value is used to decide if the object continues on this frame or not.
        /// </summary>
        public int MaxGridDistanceForObjectSwitching { get; set; }

        /// <summary>
        /// Buffer used for simplifying VideoParts (groups many in one if they are overlapping within the buffer)
        /// </summary>
        public int RectangleUnionBuffer { get; set; }

        /// <summary>
        /// If enabled, a directory will be created for each Change Percentage
        /// </summary>
        public bool Export⁬WithChangeValue { get; set; }

        // Those Properies definie the analyse area
        public int LimitLeft { get; set; }
        public int LimitRight { get; set; }
        public int LimitTop { get; set; }
        public int LimitBottom { get; set; }
        public Rectangle VideoSize { get; set; }
        public Rectangle AnalyseArea { get; set; }

        // Those properties are output directories
        public string ResultsDirectory { get; set; }
        public string AbsDiffDirectory { get; set; }
        public string FramesDirectory { get; set; }
        public string AllDetectedFramesDirectory { get; set; }

        /// <summary>
        /// Output Log-File
        /// </summary>
        public string Logfile { get; set; }

        public CompareMode CompareMode { get; set; }

        public override string ToString()
        {
            return "[" + StartDate.ToString("yyyy-MM-dd hh:mm:ss") + "] " + VideoFilename;
        }
    }
}