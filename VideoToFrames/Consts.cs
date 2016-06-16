namespace VideoToFrames
{
    public class Consts
    {
        public const bool Debug = false; // If true debug images will be generated

        public const int BigChangeVal = 250; // Default value : 600
        public const int PerformanceImprovmentFactor = 1; // NOTE : 1 is no improvment at all
        public const int NbFramesPerSecondToExport = 4;
        public const int MinChangeValueToDetectVehicle = 30;
        public const int MaxChangeValueToDetectEndOfVehicle = 10;

        public const int GridPatternFactor = 10; // For blob identify, which pixels will be tested. Default : every 10 pixel
        public const int MaxGridDistanceForObjectIdentification = 20;
    }
}