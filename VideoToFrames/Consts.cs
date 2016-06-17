namespace VideoToFrames
{
    public class Consts
    {
        public const int PerformanceImprovmentFactor = 1; // NOTE : 1 is no improvment at all
        public const int MinChangeValueToDetectVehicle = 30;
        public const int MaxChangeValueToDetectEndOfVehicle = 10;

        public const int GridPatternFactor = 30; // For blob identify, which pixels will be tested. Default : every 10 pixel
    }
}