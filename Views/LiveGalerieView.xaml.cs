using System;
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
        private int _currentIndex = -1;

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

            UpdateNavigationButtons();
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
                int index = _images.IndexOf(bitmap);
                if (index >= 0)
                {
                    ShowImageAt(index);
                }
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
            _currentIndex = -1;
            UpdateNavigationButtons();
        }

        private void ShowImageAt(int index)
        {
            if (_images == null || index < 0 || index >= _images.Count)
            {
                return;
            }

            _currentIndex = index;
            ModalImage.Source = _images[index];
            ModalOverlay.Visibility = Visibility.Visible;
            ModalOverlay.Focus();
            Keyboard.Focus(ModalOverlay);
            UpdateNavigationButtons();
        }

        private void NavigateBy(int offset)
        {
            if (_currentIndex < 0)
            {
                return;
            }

            int targetIndex = _currentIndex + offset;
            if (targetIndex < 0 || targetIndex >= _images.Count)
            {
                return;
            }

            ShowImageAt(targetIndex);
        }

        private void UpdateNavigationButtons()
        {
            if (PreviousButton == null || NextButton == null)
            {
                return;
            }

            bool hasImages = _images != null && _images.Count > 0;
            bool hasPrevious = hasImages && _currentIndex > 0;
            bool hasNext = hasImages && _currentIndex >= 0 && _currentIndex < _images.Count - 1;

            PreviousButton.IsEnabled = hasPrevious;
            NextButton.IsEnabled = hasNext;

            PreviousButton.Opacity = hasPrevious ? 1 : 0.35;
            NextButton.Opacity = hasNext ? 1 : 0.35;
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e) => NavigateBy(-1);

        private void NextButton_Click(object sender, RoutedEventArgs e) => NavigateBy(1);

        private void ModalOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (ModalOverlay.Visibility != Visibility.Visible)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Left:
                case Key.A:
                    NavigateBy(-1);
                    e.Handled = true;
                    break;
                case Key.Right:
                case Key.D:
                    NavigateBy(1);
                    e.Handled = true;
                    break;
                case Key.Escape:
                    ModalClose_Click(sender, e);
                    e.Handled = true;
                    break;
            }
        }

        private void ModalOverlay_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            double deltaX = e.TotalManipulation.Translation.X;
            if (Math.Abs(deltaX) < 40)
            {
                return;
            }

            if (deltaX > 0)
            {
                NavigateBy(-1);
            }
            else
            {
                NavigateBy(1);
            }

            e.Handled = true;
        }
    }
}
