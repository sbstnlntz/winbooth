using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FotoboxApp.Models;


namespace FotoboxApp.Services {
    public static class TemplateLoader {
        public static TemplateDefinition Load(string xmlPath) {
            var xml = XDocument.Load(xmlPath);
            var root = xml.Root ?? throw new InvalidDataException("template.xml: Root fehlt");

            var elements = root.Element("Elements") ?? throw new InvalidDataException("template.xml: <Elements> fehlt");
            var overlayElement = elements.Elements("Image").FirstOrDefault();
            if (overlayElement == null)
                throw new InvalidDataException("template.xml: Kein <Image> für Overlay gefunden");

            int width = ParseIntAttr(root, "Width", "template.xml: Attribut Width fehlt/ungültig");
            int height = ParseIntAttr(root, "Height", "template.xml: Attribut Height fehlt/ungültig");

            var overlayAttr = overlayElement.Attribute("ImagePath")?.Value;
            if (string.IsNullOrWhiteSpace(overlayAttr))
                throw new InvalidDataException("template.xml: Overlay ImagePath fehlt/leer");

            var baseDir = Path.GetDirectoryName(xmlPath) ?? string.Empty;
            var overlayPath = Path.Combine(baseDir, overlayAttr);

            var regions = new List<ImageRegion>();
            foreach (var photo in elements.Elements("Photo"))
            {
                regions.Add(new ImageRegion {
                    X = ParseIntAttr(photo, "Left", "template.xml: Photo.Left fehlt/ungültig"),
                    Y = ParseIntAttr(photo, "Top", "template.xml: Photo.Top fehlt/ungültig"),
                    Width = ParseIntAttr(photo, "Width", "template.xml: Photo.Width fehlt/ungültig"),
                    Height = ParseIntAttr(photo, "Height", "template.xml: Photo.Height fehlt/ungültig"),
                });
            }

            return new TemplateDefinition {
                Width = width,
                Height = height,
                OverlayPath = overlayPath,
                ImageRegions = regions
            };
        }

        private static int ParseIntAttr(XElement e, string name, string error)
        {
            var s = e.Attribute(name)?.Value;
            if (!int.TryParse(s, out var v))
                throw new InvalidDataException(error);
            return v;
        }
    }
}
