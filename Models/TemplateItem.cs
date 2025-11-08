// Model describing a template slot including metadata, file paths, and preview info.

using System.Windows.Media.Imaging;

namespace winbooth.Models
{
    public class TemplateItem
    {
        public string Name { get; set; }
        public string ZipPath { get; set; }
        public BitmapImage PreviewImage { get; set; }

        public override string ToString() => Name ?? base.ToString();
    }
}
