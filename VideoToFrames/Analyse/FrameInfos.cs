using System.IO;
using Emgu.CV;
using Emgu.CV.Structure;

namespace VideoToFrames.Analyse
{
    public class FrameInfos
    {
        public FrameInfos()
        {
            Area = 0;
        }

        public int Number { get; set; }
        public int Area { get; set; }
        public Image<Bgr, byte> Frame { get; set; }
        public FileInfo File { get; set; }
        public int ChangeValue { get; set; }

        public override string ToString()
        {
            return Number + " - " + Area + " - " + File.Name;
        }
    }
}