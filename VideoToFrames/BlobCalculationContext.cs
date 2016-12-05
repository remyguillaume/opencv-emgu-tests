using System.Collections.Generic;
using Emgu.CV;
using Emgu.CV.Structure;

namespace VideoToFrames
{
    public class BlobCalculationContext
    {
        public BlobCalculationContext()
        {
            BlobCoordinates = new Dictionary<int, MinAndMax>();
            NumberOfChanges = 0;
            //NumberOfPixels = 0;
        }

        public BlobCalculationContext(Image<Bgr, byte> frame, ChangeContext changeVal) : this()
        {
            Frame = frame;
            ChangeVal = changeVal;
            AlreadyTested = new bool[frame.Height, frame.Width];
        }

        // Input
        public Image<Bgr, byte> Frame { get; set; }
        public ChangeContext ChangeVal { get; set; }

        // Calculation tool
        public bool[,] AlreadyTested { get; private set; }

        // Output
        public Dictionary<int, MinAndMax> BlobCoordinates { get; private set; }
        public int NumberOfChanges { get; set; }
        //public int NumberOfPixels { get; set; }
    }

    public class MinAndMax
    {
        public int? Max;
        public int? Min;

        public bool AreMinAndMaxEqual
        {
            get { return Max.Value == Min.Value; }
        }

        public void Set(int y)
        {
            if (!Min.HasValue)
            {
                Min = y;
                Max = y;
            }
            else
            {
                if (y <= Min.Value)
                    Min = y;
                else
                    Max = y;
            }
        }
    }
}