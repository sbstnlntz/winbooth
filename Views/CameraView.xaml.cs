using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Emgu.CV;
using Emgu.CV.Structure;
using FotoboxApp.Services;
using FotoboxApp.ViewModels;
using FotoboxApp.Models;
using FotoboxApp.Utilities;

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
        private string _extractTarget;
        private string _sessionShotsDir;

        private int _currentPhotoIndex = 0;
        private readonly List<Bitmap> _capturedPhotos = new();
        private readonly List<System.Windows.Controls.Image> _previewOverlays = new();
        private bool _sessionFinished = false;

        private ImageRegion _currentRegion;

        private TaskCompletionSource<bool> _reviewDecisionSource;
        private bool _repeatLastPhoto = false;
        private void ApplyCameraRotation(Bitmap bitmap)
        {
            if (bitmap == null)
            {
                return;
            }

            if (_vm.CameraRotate180)
            {
                bitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);
            }
        }

        public CameraView(string zipPath, string galleryName, StartViewModel viewModel, bool startImmediately = false)
        {
            InitializeComponent();

            _zipPath = zipPath ?? throw new ArgumentNullException(nameof(zipPath));
            _galleryName = SanitizeGalleryName(galleryName ?? "UnbenannteGalerie");
            _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            // Session-Ordner für Einzelaufnahmen vorbereiten
            var galleryDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Fotobox", _galleryName);
            _sessionShotsDir = Path.Combine(galleryDir, "shots", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            try { Directory.CreateDirectory(_sessionShotsDir); } catch { }

            LoadTemplate(_zipPath);
            StartCamera();
            _ = StartCaptureSequence();

            // Ensure cleanup on unload
            this.Unloaded += (s, e) => CleanupResources();
        }

        private string SanitizeGalleryName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
        }

        private void LoadTemplate(string zipPath)
        {
            _extractTarget = Path.Combine(Path.GetTempPath(), "Fotobox_Template", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_extractTarget);
            ZipFile.ExtractToDirectory(zipPath, _extractTarget);

            string xmlPath = Path.Combine(_extractTarget, "template.xml");
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException("template.xml nicht gefunden in: " + _extractTarget);
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
                using var shotRaw = _frame.ToImage<Bgr, byte>().ToBitmap();
                ApplyCameraRotation(shotRaw);
                using var shot = (Bitmap)shotRaw.Clone();

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

            // Release overlay bitmap and temp folder
            CleanupResources();

            var mw = (MainWindow)Application.Current.MainWindow;
            mw.MainFrame.Navigate(new CollageView(_capturedPhotos, _zipPath, _galleryName, _vm.Direktdruck, _vm));
        }

        private void CleanupResources()
        {
            try
            {
                _capture?.Stop();
                _capture?.Dispose();
                _capture = null;
            }
            catch { }
            try
            {
                _frame?.Dispose();
                _frame = null;
            }
            catch { }
            try
            {
                _overlayBitmap?.Dispose();
                _overlayBitmap = null;
            }
            catch { }
            try
            {
                if (!string.IsNullOrEmpty(_extractTarget) && Directory.Exists(_extractTarget))
                {
                    Directory.Delete(_extractTarget, true);
                    _extractTarget = null;
                }
            }
            catch { }
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
                RegionPreviewImage.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                RegionPreviewImage.RenderTransform = new RotateTransform(region.Rotation);
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
            using (var displayBitmap = new Bitmap(_currentRegion.Width, _currentRegion.Height))
            using (Graphics g = Graphics.FromImage(displayBitmap))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(lastBitmap, 0, 0, _currentRegion.Width, _currentRegion.Height);
                var displayImage = ConvertToBitmapImage(displayBitmap);

            Application.Current.Dispatcher.Invoke(() =>
            {
                RegionPreviewImage.Source = displayImage;
                ReviewOverlay.Visibility = Visibility.Visible;
            });

            return _reviewDecisionSource.Task;
            }
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
            // Kamera-Callback wieder aktivieren, damit Livebild für nächsten Slot läuft
            if (_capture != null)
                _capture.ImageGrabbed += ProcessFrame;

            // Letztes akzeptiertes Foto in der Session speichern
            try
            {
                if (_capturedPhotos.Count > 0 && _currentRegion != null && !string.IsNullOrEmpty(_sessionShotsDir))
                {
                    var index = _currentPhotoIndex + 1; // 1-basiert für Dateiname
                    var filename = Path.Combine(_sessionShotsDir, $"shot_{index:00}.jpg");
                    using var toSave = new Bitmap(_capturedPhotos[^1]);
                    toSave.Save(filename, System.Drawing.Imaging.ImageFormat.Jpeg);
                    StatManager.RecordSinglePhoto(_galleryName);
                    _vm?.RefreshStatistics();
                }
            }
            catch { }

            _reviewDecisionSource?.SetResult(true);
        }


        private void AddPreviewImage(Bitmap bitmap, ImageRegion region)
        {
            using (var resized = new Bitmap(region.Width, region.Height))
            using (Graphics g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(bitmap, 0, 0, region.Width, region.Height);

                var img = new System.Windows.Controls.Image
                {
                    Width = region.Width,
                    Height = region.Height,
                    Stretch = Stretch.None,
                    Source = ConvertToBitmapImage(resized),
                    IsHitTestVisible = false,
                    RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                    RenderTransform = new RotateTransform(region.Rotation)
                };

                Canvas.SetLeft(img, region.X);
                Canvas.SetTop(img, region.Y);
                TemplateCanvas.Children.Insert(0, img);  // wichtig!
                _previewOverlays.Add(img);
            }
        }

        private void UpdateCountdownArc(double progress)
        {
            progress = Math.Max(0, Math.Min(1, progress));

            Dispatcher.Invoke(() =>
            {
                var arc = CountdownArcPath;
                if (arc == null)
                {
                    return;
                }

                var ringWidth = CountdownRing.ActualWidth > 0 ? CountdownRing.ActualWidth : CountdownRing.Width;
                var ringHeight = CountdownRing.ActualHeight > 0 ? CountdownRing.ActualHeight : CountdownRing.Height;
                var stroke = arc.StrokeThickness;
                var radius = Math.Max(0, Math.Min(ringWidth, ringHeight) / 2 - stroke / 2);
                var center = new System.Windows.Point(ringWidth / 2, ringHeight / 2);

                if (progress <= 0)
                {
                    arc.Data = Geometry.Empty;
                    return;
                }

                if (progress >= 1)
                {
                    arc.Data = new EllipseGeometry(center, radius, radius);
                    return;
                }

                var angle = 360 * progress;
                var angleRad = Math.PI / 180.0 * angle;
                var start = new System.Windows.Point(center.X, center.Y - radius);
                var end = new System.Windows.Point(
                    center.X + radius * Math.Sin(angleRad),
                    center.Y - radius * Math.Cos(angleRad));

                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(start, false, false);
                    ctx.ArcTo(
                        end,
                        new System.Windows.Size(radius, radius),
                        0,
                        angle > 180,
                        SweepDirection.Clockwise,
                        true,
                        false);
                }
                geometry.Freeze();
                arc.Data = geometry;
            });
        }

        private async Task AnimateCountdownStepAsync(double from, double to, int durationMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < durationMs)
            {
                var t = sw.ElapsedMilliseconds / (double)durationMs;
                var progress = from + (to - from) * t;
                UpdateCountdownArc(progress);
                await Task.Delay(16);
            }

            UpdateCountdownArc(to);
        }

        private async Task RunCountdownAsync()
        {
            var countdownSeconds = Math.Max(1, _vm.PreviewDurationSeconds);

            Dispatcher.Invoke(() =>
            {
                CountdownRing.Visibility = Visibility.Visible;
                CountdownNumber.Text = countdownSeconds.ToString();
                UpdateCountdownArc(1);
            });

            for (var i = countdownSeconds; i > 0; i--)
            {
                var current = i;
                Dispatcher.Invoke(() => CountdownNumber.Text = current.ToString());
                var from = (double)i / countdownSeconds;
                var to = (double)(i - 1) / countdownSeconds;
                await AnimateCountdownStepAsync(from, to, 1000);
            }

            Dispatcher.Invoke(() =>
            {
                CountdownRing.Visibility = Visibility.Collapsed;
                CountdownArcPath.Data = Geometry.Empty;
            });
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
            ApplyCameraRotation(bmp);
            using var resized = new Bitmap(bmp, _currentRegion.Width, _currentRegion.Height);
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
