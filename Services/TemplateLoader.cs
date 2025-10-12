using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FotoboxApp.Models;


namespace FotoboxApp.Services {
    public static class TemplateLoader {
        public static TemplateDefinition Load(string xmlPath) {
            var xml = XDocument.Load(xmlPath);
            var root = xml.Root!;
            var elements = root.Element("Elements")!;
            var overlayElement = elements.Elements("Image").FirstOrDefault();

            var definition = new TemplateDefinition {
                Width = int.Parse(root.Attribute("Width")!.Value),
                Height = int.Parse(root.Attribute("Height")!.Value),
                OverlayPath = Path.Combine(Path.GetDirectoryName(xmlPath)!, overlayElement!.Attribute("ImagePath")!.Value),
                ImageRegions = elements.Elements("Photo").Select(photo => new ImageRegion {
                    X = int.Parse(photo.Attribute("Left")!.Value),
                    Y = int.Parse(photo.Attribute("Top")!.Value),
                    Width = int.Parse(photo.Attribute("Width")!.Value),
                    Height = int.Parse(photo.Attribute("Height")!.Value),
                }).ToList()
            };

            return definition;
        }
    }
}
