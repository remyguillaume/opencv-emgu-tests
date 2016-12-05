using System.Collections.Generic;
using System.Drawing;

namespace VideoToFrames
{
    public class VideoPart
    {
        public Rectangle Rectangle { get; set; }
        public int ChangeValue { get; set; }
        public Polygon Polygon { get; set; }
    }

    public class Polygon : List<Point>
    {
    }
}