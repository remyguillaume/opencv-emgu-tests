using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Emgu.CV;
using Emgu.CV.Structure;

namespace VideoToFrames
{
    public static class BlobHelper
    {
        public static bool GetBlob(Point coordinate, Image<Bgr, byte> difference, ChangeContext changeVal, out Rectangle rectangleBlob, out Polygon polygonBlob, out int changeValue)
        {
            // We try to build the outline or the object
            // Method : Up, Right, Down, Left
            rectangleBlob = new Rectangle();
            polygonBlob = null;
            changeValue = 0;

            var context = new BlobCalculationContext(difference, changeVal);
            GetBlobCoordinatesRecursive(coordinate.X, coordinate.Y, context, 1);
            if (!ValidateBlobCoordinates(context))
                return false;

            // Blob is valid
            CoordinatesToBlobs(context.BlobCoordinates, out rectangleBlob, out polygonBlob);
            // Calculate change percentage
            changeValue = context.NumberOfChanges;
            return true;
        }

        private static bool ValidateBlobCoordinates(BlobCalculationContext context)
        {
            bool valid = false;
            foreach (var blobCoordinates in context.BlobCoordinates)
            {
                if (blobCoordinates.Value.Max.Value - blobCoordinates.Value.Min.Value > context.ChangeVal.MinimalHeightValue)
                    valid = true;
            }

            return valid;
        }

        private static void GetBlobCoordinatesRecursive(int x, int y, BlobCalculationContext context, int callStackLevel)
        {
            if (callStackLevel > Math.Max(context.Frame.Width, context.Frame.Height))
                return; // Prevents StackOverflow, because this cell will probably be reached by some other way

            if ((x < 0) || (y < 0) || (x >= context.Frame.Width) || (y >= context.Frame.Height))
                return;

            if (!context.AlreadyTested[y, x])
            {
                var cell = context.Frame[y, x];
                context.AlreadyTested[y, x] = true;
                var cellValue = cell.Blue + cell.Green + cell.Red;
                if (cellValue > context.ChangeVal.ShapeChangeValue)
                {
                    // Ok, this cell can be included in the blob
                    context.NumberOfChanges++;
                    MinAndMax minAndMax;
                    if (!context.BlobCoordinates.TryGetValue(x, out minAndMax))
                    {
                        minAndMax = new MinAndMax();
                        context.BlobCoordinates.Add(x, minAndMax);
                    }
                    minAndMax.Set(y);

                    // If this cell is still in the blob, search recursively for linked cells.
                    GetBlobCoordinatesRecursive(x, y - 1, context, callStackLevel + 1);
                    GetBlobCoordinatesRecursive(x + 1, y, context, callStackLevel + 1);
                    GetBlobCoordinatesRecursive(x, y + 1, context, callStackLevel + 1);
                    GetBlobCoordinatesRecursive(x - 1, y, context, callStackLevel + 1);
                }
            }
        }

        private static void CoordinatesToBlobs(Dictionary<int, MinAndMax> blobCoordinates, out Rectangle rectangleBlob, out Polygon polygonBlob)
        {
            rectangleBlob = new Rectangle();
            polygonBlob = new Polygon();

            if (blobCoordinates.Count == 0)
                return;

            var minX = blobCoordinates.Keys.Min();
            var maxX = blobCoordinates.Keys.Max();
            var minY = int.MaxValue;
            var maxY = 0;

            for (var x = minX; x <= maxX; ++x)
            {
                MinAndMax minAndMax;
                if (blobCoordinates.TryGetValue(x, out minAndMax))
                {
                    int y = minAndMax.Min.Value;
                    if (y < minY)
                        minY = y;
                    polygonBlob.Add(new Point(x, y));
                }
            }

            // We do not want twice the same point when we turn back, and where we start the algo
            var minXRetour = minX;
            var maxXRetour = maxX;
            if (blobCoordinates[maxX].AreMinAndMaxEqual)
                maxXRetour--;
            if (blobCoordinates[minX].AreMinAndMaxEqual)
                minXRetour++;

            for (var x = maxXRetour; x >= minXRetour; --x)
            {
                MinAndMax minAndMax;
                if (blobCoordinates.TryGetValue(x, out minAndMax))
                {
                    int y = minAndMax.Max.Value;
                    if (y > maxY)
                        maxY = y;
                    polygonBlob.Add(new Point(x, y));
                }
            }

            rectangleBlob = new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        public static void MergeBlobs(Polygon polygon1, Polygon polygon2, out Rectangle rectangleBlob, out Polygon polygonBlob, out int numberOfChanges)
        {
            var context = new BlobCalculationContext();

            // Merge parts
            MinAndMax minAndMax;
            foreach (var coordinate in polygon1.Union(polygon2))
            {
                if (!context.BlobCoordinates.TryGetValue(coordinate.X, out minAndMax))
                {
                    minAndMax = new MinAndMax();
                    context.BlobCoordinates.Add(coordinate.X, minAndMax);
                }
                minAndMax.Set(coordinate.Y);
            }

            numberOfChanges = 0;
            foreach (MinAndMax mnm in context.BlobCoordinates.Values)
                numberOfChanges += mnm.Max.Value - mnm.Min.Value + 1;

            CoordinatesToBlobs(context.BlobCoordinates, out rectangleBlob, out polygonBlob);
        }
    }
}