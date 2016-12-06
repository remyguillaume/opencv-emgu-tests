namespace VideoToFrames.Tools
{
    public class ChangeContext
    {
        public ChangeContext(int identificationChangeValue, int shapeChangeValue, int changeLimit, int minimalHeightValue, int minimalWidthValue)
        {
            IdentificationChangeValue = identificationChangeValue;
            ShapeChangeValue = shapeChangeValue;
            ChangeLimit = changeLimit;
            MinimalHeightValue = minimalHeightValue;
            MinimalWidthValue = minimalWidthValue;
        }

        /// <summary>
        /// Minimal value necessary to detect an master pixel change
        /// </summary>
        public int IdentificationChangeValue { get; private set; }

        /// <summary>
        /// Minimal Pixel value to detect neighbour pixels
        /// If a pixel of value IdentificationChangeValue is detected, the connected pixels with a value of ShapeChangeValue will be detected
        /// </summary>
        public int ShapeChangeValue { get; private set; }

        /// <summary>
        /// Miminal value of height that allow a blob to be considered as a real object
        /// </summary>
        public int MinimalHeightValue { get; private set; }

        /// <summary>
        /// Miminal value of width that allow a blob to be considered as a real object
        /// </summary>
        public int MinimalWidthValue { get; private set; }

        /// <summary>
        /// Miniamal number of pixels modifcations necessary so that an object can be considered as a real object
        /// </summary>
        public int ChangeLimit { get; set; }

        public override string ToString()
        {
            return $"{IdentificationChangeValue}.{ShapeChangeValue}.{ChangeLimit}.{MinimalHeightValue}.{MinimalWidthValue}";
        }
    }
}