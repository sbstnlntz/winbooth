using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Emgu.CV;
using Emgu.CV.Structure;
using FotoboxApp.Services;
using FotoboxApp.ViewModels;
using FotoboxApp.Models;

namespace FotoboxApp.Views
{
    public partial class CameraView : UserControl
    {
        private readonly string _zipPath;
        private readonly string _galleryName;
        private readonly StartViewModel _vm;
        private VideoCapture _capture;
        private Mat _frame;
        private TemplateDefinition _templateDefinition;
        private Bitmap _overlayBitmap;

        private int _currentPhotoIndex = 0;
        private readonly List<Bitmap> _capturedPhotos = new();
        private readonly List<System.Windows.Controls.Image> _previewOverlays = new();
        private bool _sessionFinished = false;

        private ImageRegion _currentRegion;

        private TaskCompletionSource<bool> _reviewDecisionSource;
        private bool _repeatLastPhoto = false;

        public CameraView(string zipPath, string galleryName, StartViewModel viewModel, bool startImmediately = false)
        {
            InitializeComponent();

            _zipPath = zipPath ?? throw new ArgumentNullException(nameof(zipPath));
            _galleryName = SanitizeGalleryName(galleryName ?? "UnbenannteGalerie");
            _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            LoadTemplate(_zipPath);
            StartCamera();
            _ = StartCaptureSequence();
        }

        private string SanitizeGalleryName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }

        private void LoadTemplate(string zipPath)
        {
            string extractTarget = Path.Combine(Path.GetTempPath(), "Fotobox_Template", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractTarget);
            ZipFile.ExtractToDirectory(zipPath, extractTarget);

            string xmlPath = Path.Combine(extractTarget, "template.xml");
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException("template.xml nicht gefunden in: " + extractTarget);
            _templateDefinition = TemplateLoader.Load(xmlPath);

            _overlayBitmap = new Bitmap(_templateDefinition.OverlayPath);

            Application.Current.Dispatcher.Invoke(() =>
            {
                TemplateBackground.Source = ConvertToBitmapImage(_overlayBitmap);
                TemplateCanvas.Width = _templateDefinition.Width;
                TemplateCanvas.Height = _templateDefinition.Height;
            });
        }

        private void StartCamera()
        {
            _capture?.Dispose();
            _capture = new VideoCapture(0, VideoCapture.API.DShow);
            _frame = new Mat();
            _capture.ImageGrabbed += ProcessFrame;
            _capture.Start();
        }

        private async Task StartCaptureSequence()
        {
            await Task.Delay(2000);

            while (_currentPhotoIndex < _templateDefinition.ImageRegions.Count)
            {
                InitRegionPreview();
                await RunCountdownAsync();

                if (_capture == null)
                    throw new InvalidOperationException("Kamera nicht initialisiert.");

                await ShowFlashEffectAsync();

                _capture.Grab();
                _capture.Retrieve(_frame);
                using var shot = _frame.ToImage<Bgr, byte>().ToBitmap();

                // temporär hinzufügen
                _capturedPhotos.Add((Bitmap)shot.Clone());
                AddPreviewImage(shot, _currentRegion);

                // Review anzeigen und Entscheidung abwarten
                await ShowReviewOverlayAsync();

                if (_repeatLastPhoto)
                {
                    // Letztes Bild & Overlay entfernen
                    _capturedPhotos.RemoveAt(_capturedPhotos.Count - 1);
                    var lastOverlay = _previewOverlays[^1];
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TemplateCanvas.Children.Remove(lastOverlay);
                    });
                    _previewOverlays.RemoveAt(_previewOverlays.Count - 1);
                    continue; // gleiches Foto nochmal
                }

                _currentPhotoIndex++;


            }

            _sessionFinished = true;
            _capture.ImageGrabbed -= ProcessFrame;
            _capture.Stop();
            _capture.Dispose();
            _capture = null;

            var mw = (MainWindow)Application.Current.MainWindow;
            mw.MainFrame.Navigate(new CollageView(_capturedPhotos, _zipPath, _galleryName, _vm.Direktdruck, _vm));
        }

        private void InitRegionPreview()
        {
            if (_currentPhotoIndex >= _templateDefinition.ImageRegions.Count)
                return;

            var region = _templateDefinition.ImageRegions[_currentPhotoIndex];
            _currentRegion = region;

            Application.Current.Dispatcher.Invoke(() =>
            {
                RegionPreviewImage.Width = region.Width;
                RegionPreviewImage.Height = region.Height;
                Canvas.SetLeft(RegionPreviewImage, region.X);
                Canvas.SetTop(RegionPreviewImage, region.Y);
                RegionPreviewImage.Visibility = Visibility.Visible;
            });
        }

        private Task ShowReviewOverlayAsync()
        {
            _reviewDecisionSource = new TaskCompletionSource<bool>();
            _repeatLastPhoto = false;

            // Kamera-Livebild pausieren
            if (_capture != null)
                _capture.ImageGrabbed -= ProcessFrame;

            // zuletzt geschossenes Bild holen
            var lastBitmap = _capturedPhotos[^1];
            var displayBitmap = new Bitmap(_currentRegion.Width, _currentRegion.Height);
            using (Graphics g = Graphics.FromImage(displayBitmap))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(lastBitmap, 0, 0, _currentRegion.Width, _currentRegion.Height);
            }

            var displayImage = ConvertToBitmapImage(displayBitmap);

            Application.Current.Dispatcher.Invoke(() =>
            {
                RegionPreviewImage.Source = displayImage;
                ReviewOverlay.Visibility = Visibility.Visible;
            });

            return _reviewDecisionSource.Task;
        }



        private void RepeatPhoto_Click(object sender, RoutedEventArgs e)
        {
            _repeatLastPhoto = true;

            Application.Current.Dispatcher.Invoke(() =>
            {
                ReviewOverlay.Visibility = Visibility.Collapsed;
            });

            // Kamera-Callback wieder aktivieren
            if (_capture != null)
                _capture.ImageGrabbed += ProcessFrame;

            _reviewDecisionSource?.SetResult(true);
        }


        private void ContinuePhoto_Click(object sender, RoutedEventArgs e)
        {
            _repeatLastPhoto = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                ReviewOverlay.Visibility = Visibility.Collapsed;
            });
            _reviewDecisionSource?.SetResult(true);
        }


        private void AddPreviewImage(Bitmap bitmap, ImageRegion region)
        {
            var resized = new Bitmap(_currentRegion.Width, _currentRegion.Height);
            using (Graphics g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(bitmap, 0, 0, _currentRegion.Width, _currentRegion.Height);
            }

            var img = new System.Windows.Controls.Image
            {
                Width = region.Width,
                Height = region.Height,
                Stretch = System.Windows.Media.Stretch.None,
                Source = ConvertToBitmapImage(resized),
                IsHitTestVisible = false
            };

            Canvas.SetLeft(img, region.X);
            Canvas.SetTop(img, region.Y);
            TemplateCanvas.Children.Insert(0, img);  // wichtig!
            _previewOverlays.Add(img);
        }

        private Task FadeTextBlock(TextBlock textBlock, double from, double to, int durationMs)
        {
            var animation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
            };

            var tcs = new TaskCompletionSource<bool>();
            animation.Completed += (s, e) => tcs.SetResult(true);

            Application.Current.Dispatcher.Invoke(() =>
            {
                textBlock.BeginAnimation(UIElement.OpacityProperty, animation);
            });

            return tcs.Task;
        }



        private async Task RunCountdownAsync()
        {
            int countdownSeconds = _vm.PreviewDurationSeconds;

            CountdownText.Visibility = Visibility.Visible;

            for (int i = countdownSeconds; i > 0; i--)
            {
                CountdownText.Text = i.ToString();
                await FadeTextBlock(CountdownText, 0, 1, 300); // Fade-In
                await Task.Delay(400);                         // Sichtbar
                await FadeTextBlock(CountdownText, 1, 0, 300); // Fade-Out
                await Task.Delay(100);                         // kurze Pause
            }

            CountdownText.Visibility = Visibility.Collapsed;
        }



        private async Task ShowFlashEffectAsync()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                FlashOverlay.Opacity = 1;
                FlashOverlay.Visibility = Visibility.Visible;
            });
            await Task.Delay(100);
            Application.Current.Dispatcher.Invoke(() =>
            {
                FlashOverlay.Visibility = Visibility.Collapsed;
                FlashOverlay.Opacity = 0;
            });
        }

        private void ProcessFrame(object sender, EventArgs e)
        {
            if (_sessionFinished || _currentRegion == null)
                return;

            _capture.Retrieve(_frame);
            using var bmp = _frame.ToImage<Bgr, byte>().ToBitmap();
            var resized = new Bitmap(bmp, _currentRegion.Width, _currentRegion.Height);
            var img = ConvertToBitmapImage(resized);

            Application.Current.Dispatcher.Invoke(() =>
            {
                RegionPreviewImage.Source = img;
            });
        }

        private BitmapImage ConvertToBitmapImage(Bitmap bitmap)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            var img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = ms;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _currentPhotoIndex = 0;
            _capturedPhotos.Clear();
            _sessionFinished = false;

            StartCamera();
            RegionPreviewImage.Source = null;
            foreach (var img in _previewOverlays)
                TemplateCanvas.Children.Remove(img);
            _previewOverlays.Clear();
        }
    }
}