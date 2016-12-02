namespace VideoToFrames
{
    public class ChangeDetection
    {
        public ChangeDetection(int identificationChangeValue, int shapeChangeValue)
        {
            IdentificationChangeValue = identificationChangeValue;
            ShapeChangeValue = shapeChangeValue;
        }

        public int IdentificationChangeValue { get; private set; }
        public int ShapeChangeValue { get; private set; }
    }
}