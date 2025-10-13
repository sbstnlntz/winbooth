using System.Collections.Generic;

namespace FotoboxApp.Models
{
    public class TemplateDefinition
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<ImageRegion> ImageRegions { get; set; }
        public string OverlayPath { get; set; }
    }

    public class ImageRegion
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Rotation { get; set; }
    }
}
