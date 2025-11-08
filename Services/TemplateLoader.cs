// Parses template definitions, loads assets, and normalizes metadata for runtime use.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using winbooth.Models;

namespace winbooth.Services
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

            var regions = elements.Elements("Photo")
                .Select((photo, index) => new
                {
                    Element = photo,
                    Order = GetPhotoOrder(photo, index),
                    Index = index
                })
                .OrderBy(p => p.Order)
                .ThenBy(p => p.Index)
                .Select(p => new ImageRegion
                {
                    X = ParseIntAttr(p.Element, "Left", "template.xml: Photo.Left fehlt/ungueltig"),
                    Y = ParseIntAttr(p.Element, "Top", "template.xml: Photo.Top fehlt/ungueltig"),
                    Width = ParseIntAttr(p.Element, "Width", "template.xml: Photo.Width fehlt/ungueltig"),
                    Height = ParseIntAttr(p.Element, "Height", "template.xml: Photo.Height fehlt/ungueltig"),
                    Rotation = ParseDoubleAttr(p.Element, "Rotation", 0.0),
                })
                .ToList();

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

        private static int GetPhotoOrder(XElement photo, int fallbackIndex)
        {
            static bool TryParsePositiveInt(string raw, out int value)
            {
                if (int.TryParse(raw, out value) && value > 0)
                    return true;
                value = 0;
                return false;
            }

            var numberAttr = photo.Attribute("PhotoNumber")?.Value;
            if (TryParsePositiveInt(numberAttr, out var parsed))
                return parsed;

            var nameAttr = photo.Attribute("Name")?.Value;
            if (!string.IsNullOrWhiteSpace(nameAttr))
            {
                var match = Regex.Match(nameAttr, @"\d+");
                if (match.Success && TryParsePositiveInt(match.Value, out parsed))
                    return parsed;
            }

            return fallbackIndex + 1;
        }
    }
}
