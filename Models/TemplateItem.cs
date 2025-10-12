using System.Windows.Media.Imaging;

namespace FotoboxApp.Models
{
    public class TemplateItem
    {
        public string Name { get; set; }
        public string ZipPath { get; set; }
        public BitmapImage PreviewImage { get; set; }

        public override string ToString() => Name ?? base.ToString();
    }
}
