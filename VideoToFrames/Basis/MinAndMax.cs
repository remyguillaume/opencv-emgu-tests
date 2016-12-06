namespace VideoToFrames.Basis
{
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

        public bool IsValid => Min.HasValue && Max.HasValue;
    }
}