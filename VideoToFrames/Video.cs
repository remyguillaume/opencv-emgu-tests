using System;
using System.Drawing;

namespace VideoToFrames
{
    public class Video
    {
        public Video()
        {
            LimitLeft = LimitRight = LimitTop = LimitBottom = -1;
            DebugMode = false;
            NbFramesPerSecondToExport = 4;
            MaxGridDistanceForObjectIdentification = 30;
            MinGridDistanceForObjectSwitching = 30;
        }

        public string VideoFilename { get; set; }
        public DateTime StartDate { get; set; }
        public bool DebugMode { get; set; }

        public int ChangeVal { get; set; }
        public int NbFramesPerSecondToExport { get; set; }
        public int MaxGridDistanceForObjectIdentification { get; set; }
        public int MinGridDistanceForObjectSwitching { get; set; }


        public int LimitLeft { get; set; }
        public int LimitRight { get; set; }
        public int LimitTop { get; set; }
        public int LimitBottom { get; set; }
        public Rectangle VideoSize { get; set; }
        public Rectangle AnalyseArea { get; set; }

        public string ResultsDirectory { get; set; }
        public string AbsDiffDirectory { get; set; }
        public string FramesDirectory { get; set; }
        public string AllDetectedFramesDirectory { get; set; }

        public string Logfile { get; set; }
    }
}