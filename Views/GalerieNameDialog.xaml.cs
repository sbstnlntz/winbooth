using System.Windows;

namespace FotoboxApp.Views
{
    public partial class GalerieNameDialog : Window
    {
        public string GalleryName { get; private set; }

        public GalerieNameDialog(string currentName = "")
        {
            InitializeComponent();
            GalleryNameBox.Text = currentName ?? "";
            GalleryNameBox.Focus();
            GalleryNameBox.SelectAll();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            GalleryName = GalleryNameBox.Text?.Trim();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
