// Reusable service for painting template overlays and blending captured images.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace winbooth.Services {
    public static class OverlayService {
        public static void ApplyTemplateWithImages(
            string templatePath,
            string image1Path,
            string image2Path,
            string outputPath
        ) {
            using var template = new Bitmap(templatePath);
            using var image1 = new Bitmap(image1Path);
            using var image2 = new Bitmap(image2Path);

            using var combined = new Bitmap(template.Width, template.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(combined);

            g.Clear(Color.White);

            // Target rectangles (adjust if the template layout changes).
            var target1 = new Rectangle(0, 0, template.Width / 2, template.Height / 2);
            var target2 = new Rectangle(0, template.Height / 2, template.Width / 2, template.Height / 2);

            // Draw the resized source photos.
            g.DrawImage(image1, target1);
            g.DrawImage(image2, target2);

            // Add the template overlay mask on top of the photos.
            g.DrawImage(template, new Rectangle(0, 0, template.Width, template.Height));

            // Persist the finished composition.
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            combined.Save(outputPath, ImageFormat.Jpeg);
        }
    }
}
