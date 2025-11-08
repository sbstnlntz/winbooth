using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks; // for Task.Delay
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using FotoboxApp.Models;
using FotoboxApp.Services;
using FotoboxApp.ViewModels;
using FotoboxApp.Utilities;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace FotoboxApp.Views
{
    public partial class CollageView : UserControl
    {
        private readonly List<Bitmap> _originalPhotos;
        private List<Bitmap> _currentPhotos;
        private Bitmap _overlayBitmap;
        private TemplateDefinition _templateDef;
        private Bitmap _finalBitmap;
        private string _lastSavedPath;
        private bool _isApplyingFilter;

        private readonly string _zipPath;
        private enum FilterMode
        {
            Original,
            BlackWhite,
            Sepia
        }

        private FilterMode _activeFilter = FilterMode.Original;

        private readonly string _galleryName;
        private readonly bool _direktdruck;     // ← korrekt benannt
        private readonly StartViewModel _vm;
        private string _extractTarget;

        public CollageView(
            List<Bitmap> capturedPhotos,
            string zipPath,
            string galleryName,
            bool direktdruck,                 // Parameter korrekt benannt
            StartViewModel vm)
        {
            InitializeComponent();

            // Parameter / Felder
            _zipPath = zipPath ?? throw new ArgumentNullException(nameof(zipPath));
            _galleryName = galleryName ?? "Galerie";
            _direktdruck = direktdruck;      // ← hier war vorher 'direktruck'
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            DataContext = _vm;

            // Fotos kopieren
            _originalPhotos = capturedPhotos.Select(b => (Bitmap)b.Clone()).ToList();
            _currentPhotos = new List<Bitmap>();

            // Buttons konfigurieren
            PrintButton.Visibility = _direktdruck ? Visibility.Visible : Visibility.Collapsed;
            SaveButton.Visibility = Visibility.Visible;

            Loaded += CollageView_Loaded;
            this.Unloaded += (s, e) => CleanupResources();
        }

        private async void CollageView_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= CollageView_Loaded;
            await InitializeCollageAsync();
        }

        private bool _resultHiddenByOverlay;

        private async Task InitializeCollageAsync()
        {
            ShowProcessingOverlay("Die Collage wird erstellt...", hideResultImage: true);

            var stopwatch = Stopwatch.StartNew();

            CollageInitializationData data;
            try
            {
                data = await Task.Run(BuildInitialCollageData);
            }
            catch (Exception ex)
            {
                HideProcessingOverlay();
                MessageBox.Show($"Collage konnte nicht erstellt werden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                (Application.Current.MainWindow as MainWindow)?.MainFrame.Navigate(new StartView(_vm));
                return;
            }

            _templateDef = data.TemplateDefinition;
            _overlayBitmap = data.OverlayBitmap;
            _extractTarget = data.ExtractTarget;
            _currentPhotos = data.CurrentPhotos;
            _finalBitmap = data.FinalBitmap;

            ImgResult.Source = ConvertBitmapToBitmapImage(_finalBitmap);
            PreviewOriginal.Source = ConvertBitmapToBitmapImage(data.PreviewOriginal);
            PreviewSW.Source = ConvertBitmapToBitmapImage(data.PreviewSW);
            PreviewSepia.Source = ConvertBitmapToBitmapImage(data.PreviewSepia);
            SetActiveFilter(FilterMode.Original);

            data.PreviewOriginal?.Dispose();
            data.PreviewSW?.Dispose();
            data.PreviewSepia?.Dispose();

            var savedPath = SaveToGallery(_finalBitmap, forceNewFile: true);
            if (!string.IsNullOrEmpty(savedPath))
            {
                _vm.HandleGalleryFileSaved(savedPath);
            }
            else
            {
                MessageBox.Show("Die Collage konnte nicht automatisch gespeichert werden. Bitte manuell speichern.", "Speicherhinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var desiredDelay = _vm?.CollageCreationDelayMilliseconds ?? 0;
            var elapsed = (int)stopwatch.ElapsedMilliseconds;
            stopwatch.Stop();
            if (desiredDelay > elapsed)
            {
                await Task.Delay(desiredDelay - elapsed);
            }

            HideProcessingOverlay();
        }
        private CollageInitializationData BuildInitialCollageData()
        {
            var data = new CollageInitializationData();

            data.ExtractTarget = Path.Combine(
                Path.GetTempPath(),
                "Fotobox_Template",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(data.ExtractTarget);
            ZipFile.ExtractToDirectory(_zipPath, data.ExtractTarget);

            var xmlPath = Path.Combine(data.ExtractTarget, "template.xml");
            data.TemplateDefinition = TemplateLoader.Load(xmlPath);
            data.OverlayBitmap = new Bitmap(data.TemplateDefinition.OverlayPath);

            data.CurrentPhotos = _originalPhotos.Select(b => (Bitmap)b.Clone()).ToList();
            data.FinalBitmap = ComposeFinalBitmap(data.CurrentPhotos, data.OverlayBitmap, data.TemplateDefinition);

            var previewOriginalPhotos = _originalPhotos.Select(b => (Bitmap)b.Clone()).ToList();
            data.PreviewOriginal = ComposeFinalBitmap(previewOriginalPhotos, data.OverlayBitmap, data.TemplateDefinition, 0.18);
            DisposeBitmapList(previewOriginalPhotos);

            var previewSwPhotos = _originalPhotos.Select(b => MakeBlackWhite((Bitmap)b.Clone())).ToList();
            data.PreviewSW = ComposeFinalBitmap(previewSwPhotos, data.OverlayBitmap, data.TemplateDefinition, 0.18);
            DisposeBitmapList(previewSwPhotos);

            var previewSepiaPhotos = _originalPhotos.Select(b => MakeSepia((Bitmap)b.Clone())).ToList();
            data.PreviewSepia = ComposeFinalBitmap(previewSepiaPhotos, data.OverlayBitmap, data.TemplateDefinition, 0.18);
            DisposeBitmapList(previewSepiaPhotos);


            return data;
        }

        private static void DisposeBitmapList(IEnumerable<Bitmap> bitmaps)
        {
            if (bitmaps == null)
                return;

            foreach (var bmp in bitmaps)
            {
                try { bmp?.Dispose(); } catch { }
            }
        }

        private sealed class CollageInitializationData
        {
            public TemplateDefinition TemplateDefinition { get; set; }
            public Bitmap OverlayBitmap { get; set; }
            public List<Bitmap> CurrentPhotos { get; set; }
            public Bitmap FinalBitmap { get; set; }
            public Bitmap PreviewOriginal { get; set; }
            public Bitmap PreviewSW { get; set; }
            public Bitmap PreviewSepia { get; set; }
            public string ExtractTarget { get; set; }
        }

        private void CleanupResources()
        {
            try { _finalBitmap?.Dispose(); _finalBitmap = null; } catch { }
            try { foreach (var b in _currentPhotos) b?.Dispose(); } catch { }
            try { foreach (var b in _originalPhotos) b?.Dispose(); } catch { }
            try { _overlayBitmap?.Dispose(); } catch { }
            try { if (!string.IsNullOrEmpty(_extractTarget) && Directory.Exists(_extractTarget)) Directory.Delete(_extractTarget, true); } catch { }
        }

        private Task ApplyPostProcessDelayAsync()
        {
            var delay = Math.Max(0, _vm?.PostProcessDelayMilliseconds ?? 0);
            return delay > 0
                ? Task.Delay(delay)
                : Task.CompletedTask;
        }

        private void ShowProcessingOverlay(string message, bool hideResultImage = false)
        {
            ProcessingOverlayText.Text = string.IsNullOrWhiteSpace(message)
                ? "Bild wird gespeichert..."
                : message;
            ProcessingOverlay.Visibility = Visibility.Visible;
            if (hideResultImage)
            {
                ImgResult.Visibility = Visibility.Hidden;
                _resultHiddenByOverlay = true;
            }
        }

        private void HideProcessingOverlay()
        {
            ProcessingOverlay.Visibility = Visibility.Collapsed;
            if (_resultHiddenByOverlay)
            {
                ImgResult.Visibility = Visibility.Visible;
                _resultHiddenByOverlay = false;
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            (Application.Current.MainWindow as MainWindow)?
                .MainFrame.Navigate(new CameraView(_zipPath, _galleryName, _vm));
        }

        private async void Print_Click(object sender, RoutedEventArgs e)
        {
            ShowProcessingOverlay("Bild wird gedruckt...");

            var savedPath = SaveToGallery(_finalBitmap);
            if (savedPath == null)
            {
                HideProcessingOverlay();
                return;
            }

            _vm.HandleGalleryFileSaved(savedPath);

            try
            {
                SendToPrinter(_finalBitmap);
                StatManager.RecordCollagePrinted(_galleryName);
                _vm?.RefreshStatistics();
            }
            catch (Exception ex)
            {
                HideProcessingOverlay();
                MessageBox.Show($"Fehler beim Drucken:\n{ex}", "Druckfehler",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await ApplyPostProcessDelayAsync();

            (Application.Current.MainWindow as MainWindow)?
                .MainFrame.Navigate(new StartView(_vm));
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            ShowProcessingOverlay("Bild wird gespeichert...");

            var savedPath = SaveToGallery(_finalBitmap);
            if (savedPath == null)
            {
                HideProcessingOverlay();
                return;
            }

            _vm.HandleGalleryFileSaved(savedPath);

            await ApplyPostProcessDelayAsync();

            (Application.Current.MainWindow as MainWindow)?
                .MainFrame.Navigate(new StartView(_vm));
        }

        // ---- FILTER-Buttons ----

        private async void FilterOriginal_Click(object sender, RoutedEventArgs e)
        {
            await ApplyFilterAsync(FilterMode.Original, static bmp => bmp, "Original wird wiederhergestellt...");
        }

        private async void FilterSW_Click(object sender, RoutedEventArgs e)
        {
            await ApplyFilterAsync(FilterMode.BlackWhite, MakeBlackWhite, "Schwarz-Weiß-Filter wird angewendet...");
        }

        private async void FilterSepia_Click(object sender, RoutedEventArgs e)
        {
            await ApplyFilterAsync(FilterMode.Sepia, MakeSepia, "Sepia-Filter wird angewendet...");
        }

        private void ReplaceCurrentPhotos(List<Bitmap> newPhotos)
        {
            DisposeBitmapList(_currentPhotos);
            _currentPhotos = newPhotos ?? new List<Bitmap>();
        }

        private async Task ApplyFilterAsync(FilterMode mode, Func<Bitmap, Bitmap> transform, string statusMessage)
        {
            if (_isApplyingFilter)
            {
                return;
            }

            _isApplyingFilter = true;
            ShowProcessingOverlay(string.IsNullOrWhiteSpace(statusMessage) ? "Filter wird angewendet..." : statusMessage);

            try
            {
                var filteredPhotos = await Task.Run(() =>
                    _originalPhotos
                        .Select(original =>
                        {
                            var clone = (Bitmap)original.Clone();
                            return transform(clone);
                        })
                        .ToList());

                ReplaceCurrentPhotos(filteredPhotos);
                await UpdateFinalBitmapAsync();
                SetActiveFilter(mode);
                ResaveCurrentCollageIfPossible();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Der Filter konnte nicht angewendet werden:\n{ex.Message}", "Filterfehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideProcessingOverlay();
                _isApplyingFilter = false;
            }
        }

        private void SetActiveFilter(FilterMode mode)
        {
            _activeFilter = mode;
            Dispatcher.Invoke(() =>
            {
                BorderOriginal.Fill = mode == FilterMode.Original
                    ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 198, 255))
                    : System.Windows.Media.Brushes.Transparent;

                BorderSW.Fill = mode == FilterMode.BlackWhite
                    ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 198, 255))
                    : System.Windows.Media.Brushes.Transparent;

                BorderSepia.Fill = mode == FilterMode.Sepia
                    ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 198, 255))
                    : System.Windows.Media.Brushes.Transparent;
            });
        }

        private async Task UpdateFinalBitmapAsync()
        {
            if (_overlayBitmap == null || _templateDef == null)
                return;

            var newBitmap = await Task.Run(() => ComposeFinalBitmap(_currentPhotos, _overlayBitmap, _templateDef));
            var imageSource = ConvertBitmapToBitmapImage(newBitmap);

            await Dispatcher.InvokeAsync(() =>
            {
                _finalBitmap?.Dispose();
                _finalBitmap = newBitmap;
                ImgResult.Source = imageSource;
            }, DispatcherPriority.Render);
        }

        private void ResaveCurrentCollageIfPossible()
        {
            if (string.IsNullOrEmpty(_lastSavedPath))
                return;

            SaveToGallery(_finalBitmap);
        }

        // ---- Collage erzeugen ----

        private Bitmap ComposeFinalBitmap(
            List<Bitmap> fotos,
            Bitmap overlay,
            TemplateDefinition template,
            double scale = 1.0)
        {
            int w = (int)(template.Width * scale);
            int h = (int)(template.Height * scale);
            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.Clear(System.Drawing.Color.White);

            for (int i = 0; i < template.ImageRegions.Count; i++)
            {
                var r = template.ImageRegions[i];
                if (i >= fotos.Count || fotos[i] == null)
                    continue;

                var targetX = (float)(r.X * scale);
                var targetY = (float)(r.Y * scale);
                var targetWidth = (float)(r.Width * scale);
                var targetHeight = (float)(r.Height * scale);

                if (Math.Abs(r.Rotation) > 0.001)
                {
                    var state = g.Save();
                    g.TranslateTransform(targetX + targetWidth / 2f, targetY + targetHeight / 2f);
                    g.RotateTransform((float)r.Rotation);
                    g.DrawImage(fotos[i], -targetWidth / 2f, -targetHeight / 2f, targetWidth, targetHeight);
                    g.Restore(state);
                }
                else
                {
                    g.DrawImage(fotos[i], targetX, targetY, targetWidth, targetHeight);
                }
            }

            g.DrawImage(overlay, new Rectangle(0, 0, w, h));
            return bmp;
        }

        private static Bitmap MakeBlackWhite(Bitmap bmp)
        {
            return ApplyPixelTransform(bmp, static (r, g, b) =>
            {
                var luminance = (byte)Math.Clamp((int)(r * 0.299 + g * 0.587 + b * 0.114), 0, 255);
                return (luminance, luminance, luminance);
            });
        }

        private static Bitmap MakeSepia(Bitmap bmp)
        {
            return ApplyPixelTransform(bmp, static (r, g, b) =>
            {
                int tr = (int)(0.393 * r + 0.769 * g + 0.189 * b);
                int tg = (int)(0.349 * r + 0.686 * g + 0.168 * b);
                int tb = (int)(0.272 * r + 0.534 * g + 0.131 * b);

                byte nr = (byte)Math.Clamp(tr, 0, 255);
                byte ng = (byte)Math.Clamp(tg, 0, 255);
                byte nb = (byte)Math.Clamp(tb, 0, 255);
                return (nr, ng, nb);
            });
        }

        private unsafe static Bitmap ApplyPixelTransform(Bitmap bitmap, Func<byte, byte, byte, (byte r, byte g, byte b)> transform)
        {
            bitmap = EnsureEditableFormat(bitmap);
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);

            try
            {
                int height = data.Height;
                int width = data.Width;
                int stride = data.Stride;
                int pixelSize = System.Drawing.Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;

                byte* scan0 = (byte*)data.Scan0;
                Parallel.For(0, height, y =>
                {
                    byte* row = scan0 + y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        byte* pixel = row + x * pixelSize;
                        var (r, g, b) = transform(pixel[2], pixel[1], pixel[0]);
                        pixel[2] = r;
                        pixel[1] = g;
                        pixel[0] = b;
                    }
                });
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }

        private static Bitmap EnsureEditableFormat(Bitmap bitmap)
        {
            var format = bitmap.PixelFormat;
            if (format == DrawingPixelFormat.Format24bppRgb
                || format == DrawingPixelFormat.Format32bppArgb
                || format == DrawingPixelFormat.Format32bppPArgb
                || format == DrawingPixelFormat.Format32bppRgb)
            {
                return bitmap;
            }

            var converted = new Bitmap(bitmap.Width, bitmap.Height, DrawingPixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(converted))
            {
                g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
            }

            bitmap.Dispose();
            return converted;
        }

        private string SaveToGallery(Bitmap bmp, bool forceNewFile = false)
        {
            try
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Fotobox", _galleryName);
                Directory.CreateDirectory(folder);

                var reuseExisting = !forceNewFile
                                    && !string.IsNullOrEmpty(_lastSavedPath)
                                    && File.Exists(_lastSavedPath);

                string path;
                if (reuseExisting)
                {
                    path = _lastSavedPath;
                }
                else
                {
                    var filename = $"{_galleryName}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    path = Path.Combine(folder, filename);
                }

                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);

                if (!reuseExisting)
                {
                    StatManager.RecordCollageCreated(_galleryName);
                    _vm?.RefreshStatistics();
                }

                _lastSavedPath = path;
                return path;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex}", "Speicherfehler",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private BitmapImage ConvertBitmapToBitmapImage(Bitmap bmp)
        {
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            var img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = ms;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }

        private void SendToPrinter(Bitmap bmp)
        {
            using var pd = new PrintDocument();
            pd.PrinterSettings.PrinterName = _vm.SelectedPrinterName;
            pd.PrintPage += (s, ev) =>
            {
                var mb = ev.MarginBounds;
                float scale = Math.Min(
                    (float)mb.Width / bmp.Width,
                    (float)mb.Height / bmp.Height);
                int w = (int)(bmp.Width * scale);
                int h = (int)(bmp.Height * scale);
                int x = mb.X + (mb.Width - w) / 2;
                int y = mb.Y + (mb.Height - h) / 2;
                ev.Graphics.DrawImage(bmp, x, y, w, h);
            };
            pd.Print();
        }
    }
}


