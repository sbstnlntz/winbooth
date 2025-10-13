using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;             // ← für Task.Delay
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using FotoboxApp.Models;
using FotoboxApp.Services;
using FotoboxApp.ViewModels;

namespace FotoboxApp.Views
{
    public partial class CollageView : UserControl
    {
        private readonly List<Bitmap> _originalPhotos;
        private List<Bitmap> _currentPhotos;
        private readonly Bitmap _overlayBitmap;
        private readonly TemplateDefinition _templateDef;
        private Bitmap _finalBitmap;

        private readonly string _zipPath;
        private readonly string _galleryName;
        private readonly bool _direktdruck;     // ← korrekt benannt
        private readonly StartViewModel _vm;
        private readonly string _extractTarget;

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
            _currentPhotos = _originalPhotos.Select(b => (Bitmap)b.Clone()).ToList();

            // Template/Overlay laden
            var extractTarget = Path.Combine(
                Path.GetTempPath(),
                "Fotobox_Template",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractTarget);
            ZipFile.ExtractToDirectory(_zipPath, extractTarget);
            _extractTarget = extractTarget;

            var xmlPath = Path.Combine(extractTarget, "template.xml");
            _templateDef = TemplateLoader.Load(xmlPath);
            using var ovBmp = new Bitmap(_templateDef.OverlayPath);
            _overlayBitmap = new Bitmap(ovBmp);

            // Erste Collage rendern
            _finalBitmap = ComposeFinalBitmap(_currentPhotos, _overlayBitmap, _templateDef);
            ImgResult.Source = ConvertBitmapToBitmapImage(_finalBitmap);

            // Vorschau-Thumbnails
            PreviewOriginal.Source = ConvertBitmapToBitmapImage(
                ComposeFinalBitmap(_originalPhotos, _overlayBitmap, _templateDef, 0.18));
            PreviewSW.Source = ConvertBitmapToBitmapImage(
                ComposeFinalBitmap(
                    _originalPhotos.Select(b => MakeBlackWhite((Bitmap)b.Clone())).ToList(),
                    _overlayBitmap,
                    _templateDef,
                    0.18));
            PreviewSepia.Source = ConvertBitmapToBitmapImage(
                ComposeFinalBitmap(
                    _originalPhotos.Select(b => MakeSepia((Bitmap)b.Clone())).ToList(),
                    _overlayBitmap,
                    _templateDef,
                    0.18));

            // Buttons konfigurieren
            PrintButton.Visibility = _direktdruck ? Visibility.Visible : Visibility.Collapsed;
            SaveButton.Visibility = Visibility.Visible;
            this.Unloaded += (s, e) => CleanupResources();
        }

        private void CleanupResources()
        {
            try { _finalBitmap?.Dispose(); _finalBitmap = null; } catch { }
            try { foreach (var b in _currentPhotos) b?.Dispose(); } catch { }
            try { foreach (var b in _originalPhotos) b?.Dispose(); } catch { }
            try { _overlayBitmap?.Dispose(); } catch { }
            try { if (!string.IsNullOrEmpty(_extractTarget) && Directory.Exists(_extractTarget)) Directory.Delete(_extractTarget, true); } catch { }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            (Application.Current.MainWindow as MainWindow)?
                .MainFrame.Navigate(new CameraView(_zipPath, _galleryName, _vm));
        }

        private async void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveToGallery(_finalBitmap);
                SendToPrinter(_finalBitmap);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Drucken:\n{ex}", "Druckfehler",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }

            NotificationOverlay.Visibility = Visibility.Visible;
            await Task.Delay(3000);  // ← Task.Delay benötigt System.Threading.Tasks

            (Application.Current.MainWindow as MainWindow)?
                .MainFrame.Navigate(new StartView(_vm));
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveToGallery(_finalBitmap);
            (Application.Current.MainWindow as MainWindow)?
                .MainFrame.Navigate(new StartView(_vm));
        }

        private void Gallery_Click(object sender, RoutedEventArgs e)
        {
            // Passe an, wenn du eine echte GalleryView hast
            (Application.Current.MainWindow as MainWindow)?
                .MainFrame.Navigate(new StartView(_vm));
        }

        // ---- FILTER-Buttons ----

        private void FilterOriginal_Click(object sender, RoutedEventArgs e)
        {
            _currentPhotos = _originalPhotos.Select(b => (Bitmap)b.Clone()).ToList();
            UpdateFinalBitmap();
        }

        private void FilterSW_Click(object sender, RoutedEventArgs e)
        {
            _currentPhotos = _originalPhotos.Select(b => MakeBlackWhite((Bitmap)b.Clone())).ToList();
            UpdateFinalBitmap();
        }

        private void FilterSepia_Click(object sender, RoutedEventArgs e)
        {
            _currentPhotos = _originalPhotos.Select(b => MakeSepia((Bitmap)b.Clone())).ToList();
            UpdateFinalBitmap();
        }

        private void UpdateFinalBitmap()
        {
            _finalBitmap = ComposeFinalBitmap(_currentPhotos, _overlayBitmap, _templateDef);
            ImgResult.Source = ConvertBitmapToBitmapImage(_finalBitmap);
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

        private Bitmap MakeBlackWhite(Bitmap bmp)
        {
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    int l = (int)(c.R * .299 + c.G * .587 + c.B * .114);
                    bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(l, l, l));
                }
            return bmp;
        }

        private Bitmap MakeSepia(Bitmap bmp)
        {
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    int tr = (int)(.393 * c.R + .769 * c.G + .189 * c.B);
                    int tg = (int)(.349 * c.R + .686 * c.G + .168 * c.B);
                    int tb = (int)(.272 * c.R + .534 * c.G + .131 * c.B);
                    bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(
                        Math.Min(tr, 255),
                        Math.Min(tg, 255),
                        Math.Min(tb, 255)));
                }
            return bmp;
        }

        private void SaveToGallery(Bitmap bmp)
        {
            try
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "Fotobox", _galleryName);
                Directory.CreateDirectory(folder);

                var filename = $"{_galleryName}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                bmp.Save(Path.Combine(folder, filename), System.Drawing.Imaging.ImageFormat.Jpeg);
                Utilities.StatManager.IncreaseTotalPhotoCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex}", "Speicherfehler",
                                MessageBoxButton.OK, MessageBoxImage.Error);
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
