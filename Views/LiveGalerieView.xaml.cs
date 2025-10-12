using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Input;

namespace FotoboxApp.Views
{
    public partial class LiveGalerieView : UserControl
    {
        private readonly string _zipPath;
        private readonly string _galleryName;
        private List<BitmapImage> _images;

        public LiveGalerieView(string zipPath, string galleryName)
        {
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("LiveGalerieView initialisiert!"); // Debug 1
            _zipPath = zipPath;
            _galleryName = galleryName;

            // KORREKTEN Galerie-Ordner benutzen!
            string galerieDir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures), "Fotobox", _galleryName);

            _images = new List<BitmapImage>();

            if (Directory.Exists(galerieDir))
            {
                foreach (var file in Directory.GetFiles(galerieDir, "*.jpg"))
                {
                    _images.Add(LoadBitmapImage(file));
                }
            }

            GalleryItems.ItemsSource = _images;
            System.Diagnostics.Debug.WriteLine("Images geladen und ItemsSource gesetzt: " + _images.Count); // Debug 2

            // Der wichtige Trick:
            GalleryItems.AddHandler(Image.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(Image_MouseLeftButtonUp), true);
            System.Diagnostics.Debug.WriteLine("AddHandler gesetzt!"); // Debug 3
        }


        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Galerie geschlossen."); // Debug 4
            var mw = (MainWindow)System.Windows.Application.Current.MainWindow;
            mw.MainFrame.Navigate(new StartView(mw.MainViewModel));

        }


        private BitmapImage LoadBitmapImage(string path)
        {
            var img = new BitmapImage();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                img.BeginInit();
                img.StreamSource = fs;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
            }
            return img;
        }

        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Klick auf Bild erkannt!"); // Debug 5
            if (e.OriginalSource is Image img && img.Source is BitmapImage bitmap)
            {
                System.Diagnostics.Debug.WriteLine("ModalOverlay sichtbar gemacht!"); // Debug 6
                ModalImage.Source = bitmap;
                ModalOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("e.OriginalSource ist KEIN Image! Typ: " + e.OriginalSource.GetType().Name); // Debug 7
            }
        }

        private void ModalClose_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Modal geschlossen!"); // Debug 8
            ModalOverlay.Visibility = Visibility.Collapsed;
            ModalImage.Source = null;
        }
    }
}
