using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FotoboxApp.Models;

namespace FotoboxApp.Services
{
    public static class TemplateLoader
    {
        public static TemplateDefinition Load(string xmlPath)
        {
            var xml = XDocument.Load(xmlPath);
            var root = xml.Root ?? throw new InvalidDataException("template.xml: Root fehlt");

            var elements = root.Element("Elements") ?? throw new InvalidDataException("template.xml: <Elements> fehlt");
            var overlayElement = elements.Elements("Image").FirstOrDefault();
            if (overlayElement == null)
                throw new InvalidDataException("template.xml: Kein <Image> fuer Overlay gefunden");

            int width = ParseIntAttr(root, "Width", "template.xml: Attribut Width fehlt/ungueltig");
            int height = ParseIntAttr(root, "Height", "template.xml: Attribut Height fehlt/ungueltig");

            var overlayAttr = overlayElement.Attribute("ImagePath")?.Value;
            if (string.IsNullOrWhiteSpace(overlayAttr))
                throw new InvalidDataException("template.xml: Overlay ImagePath fehlt/leer");

            var baseDir = Path.GetDirectoryName(xmlPath) ?? string.Empty;
            var overlayPath = Path.Combine(baseDir, overlayAttr);

            var regions = new List<ImageRegion>();
            foreach (var photo in elements.Elements("Photo"))
            {
                regions.Add(new ImageRegion
                {
                    X = ParseIntAttr(photo, "Left", "template.xml: Photo.Left fehlt/ungueltig"),
                    Y = ParseIntAttr(photo, "Top", "template.xml: Photo.Top fehlt/ungueltig"),
                    Width = ParseIntAttr(photo, "Width", "template.xml: Photo.Width fehlt/ungueltig"),
                    Height = ParseIntAttr(photo, "Height", "template.xml: Photo.Height fehlt/ungueltig"),
                    Rotation = ParseDoubleAttr(photo, "Rotation", 0.0),
                });
            }

            return new TemplateDefinition
            {
                Width = width,
                Height = height,
                OverlayPath = overlayPath,
                ImageRegions = regions,
            };
        }

        private static int ParseIntAttr(XElement element, string name, string errorMessage)
        {
            var raw = element.Attribute(name)?.Value;
            if (!int.TryParse(raw, out var value))
                throw new InvalidDataException(errorMessage);
            return value;
        }

        private static double ParseDoubleAttr(XElement element, string name, double defaultValue)
        {
            var raw = element.Attribute(name)?.Value;
            if (string.IsNullOrWhiteSpace(raw))
                return defaultValue;

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return value;

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
                return value;

            return defaultValue;
        }
    }
}
