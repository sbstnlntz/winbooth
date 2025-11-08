// StartViewModel logic for overlay media, instructional assets, and background rendering tasks.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using winbooth.Services;

namespace winbooth.ViewModels
{
    public partial class StartViewModel
    {
        private static readonly string GraphicsAssetsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Fotobox");
        private static readonly string[] CustomGraphicExtensions = { ".png", ".jpg", ".jpeg" };

        private static readonly Uri DefaultStartInstructionUri = new("pack://application:,,,/winbooth;component/Assets/start_instructions.png", UriKind.Absolute);
        private static readonly Uri DefaultWarningInfoUri = new("pack://application:,,,/winbooth;component/Assets/warning_hint.png", UriKind.Absolute);
        private const string CustomStartInstructionFilePrefix = "start_instruction_custom";
        private const string CustomWarningInfoFilePrefix = "warning_hint_custom";
        private const int GraphicsDefaultsVersion = 2025020601;
        private static readonly string GraphicsConfigFolder = AppStorage.EnsureDirectory("graphics");
        private static readonly string GraphicsVersionMarkerPath = Path.Combine(GraphicsConfigFolder, "graphics_defaults.version");
        private static readonly string LegacyGraphicsVersionMarkerPath = Path.Combine(GraphicsAssetsFolder, "graphics_defaults.version");
        private static bool _graphicsMarkerMigrated;

        private Brush _startInstructionBrush;
        private string _startInstructionDescription = "Standardgrafik";
        private bool _hasCustomStartInstruction;

        private Brush _warningInfoBrush;
        private string _warningInfoDescription = "Standardgrafik";
        private bool _hasCustomWarningInfo;

        public Brush StartInstructionBrush
        {
            get => _startInstructionBrush;
            private set
            {
                if (Equals(_startInstructionBrush, value))
                    return;

                _startInstructionBrush = value;
                OnPropertyChanged();
            }
        }

        public string StartInstructionDescription
        {
            get => _startInstructionDescription;
            private set
            {
                if (_startInstructionDescription == value)
                    return;

                _startInstructionDescription = value;
                OnPropertyChanged();
            }
        }

        public bool HasCustomStartInstruction
        {
            get => _hasCustomStartInstruction;
            private set
            {
                if (_hasCustomStartInstruction == value)
                    return;

                _hasCustomStartInstruction = value;
                OnPropertyChanged();
            }
        }

        public Brush WarningInfoBrush
        {
            get => _warningInfoBrush;
            private set
            {
                if (Equals(_warningInfoBrush, value))
                    return;

                _warningInfoBrush = value;
                OnPropertyChanged();
            }
        }

        public string WarningInfoDescription
        {
            get => _warningInfoDescription;
            private set
            {
                if (_warningInfoDescription == value)
                    return;

                _warningInfoDescription = value;
                OnPropertyChanged();
            }
        }

        public bool HasCustomWarningInfo
        {
            get => _hasCustomWarningInfo;
            private set
            {
                if (_hasCustomWarningInfo == value)
                    return;

                _hasCustomWarningInfo = value;
                OnPropertyChanged();
            }
        }

        private static string GetCustomGraphicPath(string filePrefix, string extension) =>
            Path.Combine(GraphicsAssetsFolder, $"{filePrefix}{extension}");

        private static string FindExistingCustomGraphic(string filePrefix)
        {
            foreach (var extension in CustomGraphicExtensions)
            {
                var candidate = GetCustomGraphicPath(filePrefix, extension);
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static Brush CreateImageBrushFromFile(string path)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                image.EndInit();
                image.Freeze();

                var brush = new ImageBrush(image)
                {
                    Stretch = Stretch.Uniform,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Top
                };
                brush.Freeze();
                return brush;
            }
            catch
            {
                return null;
            }
        }

        private static Brush CreateImageBrushFromUri(Uri uri)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = uri;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();

                var brush = new ImageBrush(image)
                {
                    Stretch = Stretch.Uniform,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Top
                };
                brush.Freeze();
                return brush;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapImage LoadBitmapImage(Uri uri)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = uri;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private static Brush EnsureFrozenBrush(Brush brush)
        {
            if (brush is Freezable freezable && !freezable.IsFrozen)
            {
                freezable.Freeze();
            }

            return brush;
        }

        private (Brush Brush, string Description, bool HasCustom) LoadGraphicResource(Uri defaultUri, string filePrefix)
        {
            var customPath = FindExistingCustomGraphic(filePrefix);
            if (!string.IsNullOrEmpty(customPath))
            {
                var brush = CreateImageBrushFromFile(customPath);
                if (brush != null)
                {
                    var description = $"Benutzerdefiniert ({Path.GetExtension(customPath)?.Trim('.').ToUpperInvariant()})";
                    return (EnsureFrozenBrush(brush), description, true);
                }
            }

            var defaultBrush = CreateImageBrushFromUri(defaultUri);
            if (defaultBrush != null)
            {
                return (EnsureFrozenBrush(defaultBrush), "Standardgrafik", false);
            }

            var fallback = new SolidColorBrush(Color.FromRgb(245, 245, 245));
            return (EnsureFrozenBrush(fallback), "Standardgrafik", false);
        }

        private void RefreshStartInstructionGraphic()
        {
            var info = LoadGraphicResource(DefaultStartInstructionUri, CustomStartInstructionFilePrefix);
            StartInstructionBrush = info.Brush;
            StartInstructionDescription = info.Description;
            HasCustomStartInstruction = info.HasCustom;
        }

        private void RefreshWarningInfoGraphic()
        {
            var info = LoadGraphicResource(DefaultWarningInfoUri, CustomWarningInfoFilePrefix);
            WarningInfoBrush = info.Brush;
            WarningInfoDescription = info.Description;
            HasCustomWarningInfo = info.HasCustom;
        }

        private bool TryUpdateGraphicResource(string filePrefix, string sourceFilePath, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                errorMessage = "Die ausgewählte Datei existiert nicht.";
                return false;
            }

            var extension = Path.GetExtension(sourceFilePath)?.ToLowerInvariant();
            if (extension == null || !CustomGraphicExtensions.Contains(extension))
            {
                errorMessage = "Bitte eine PNG- oder JPG-Datei wählen.";
                return false;
            }

            try
            {
                if (!string.IsNullOrEmpty(GraphicsAssetsFolder))
                {
                    Directory.CreateDirectory(GraphicsAssetsFolder);
                }

                foreach (var ext in CustomGraphicExtensions)
                {
                    var candidate = GetCustomGraphicPath(filePrefix, ext);
                    if (File.Exists(candidate))
                    {
                        File.Delete(candidate);
                    }
                }

                var destination = GetCustomGraphicPath(filePrefix, extension);
                File.Copy(sourceFilePath, destination, overwrite: true);
            }
            catch (Exception ex)
            {
                errorMessage = $"Die Grafik konnte nicht übernommen werden: {ex.Message}";
                return false;
            }

            return true;
        }

        private bool TryResetGraphicResource(string filePrefix, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                foreach (var ext in CustomGraphicExtensions)
                {
                    var candidate = GetCustomGraphicPath(filePrefix, ext);
                    if (File.Exists(candidate))
                    {
                        File.Delete(candidate);
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Die benutzerdefinierte Grafik konnte nicht entfernt werden: {ex.Message}";
                return false;
            }

            return true;
        }

        private void EnsureDefaultGraphicsActive()
        {
            try
            {
                if (string.IsNullOrEmpty(GraphicsAssetsFolder))
                    return;

                EnsureGraphicsMarkerMigrated();

                var appliedVersion = 0;
                if (File.Exists(GraphicsVersionMarkerPath))
                {
                    var content = File.ReadAllText(GraphicsVersionMarkerPath).Trim();
                    int.TryParse(content, NumberStyles.Integer, CultureInfo.InvariantCulture, out appliedVersion);
                }

                if (appliedVersion >= GraphicsDefaultsVersion)
                    return;

                Directory.CreateDirectory(GraphicsAssetsFolder);
                TryResetGraphicResource(CustomStartInstructionFilePrefix, out _);
                TryResetGraphicResource(CustomWarningInfoFilePrefix, out _);
                File.WriteAllText(GraphicsVersionMarkerPath, GraphicsDefaultsVersion.ToString(CultureInfo.InvariantCulture));
            }
            catch
            {
                // Ignore failures and keep the previous graphics so startup is not interrupted.
            }
        }

        private static void EnsureGraphicsMarkerMigrated()
        {
            if (_graphicsMarkerMigrated)
                return;

            _graphicsMarkerMigrated = true;

            try
            {
                if (File.Exists(GraphicsVersionMarkerPath))
                    return;

                if (File.Exists(LegacyGraphicsVersionMarkerPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(GraphicsVersionMarkerPath)!);
                    File.Copy(LegacyGraphicsVersionMarkerPath, GraphicsVersionMarkerPath, overwrite: false);
                    try { File.Delete(LegacyGraphicsVersionMarkerPath); } catch { }
                }
            }
            catch
            {
                // migration best effort
            }
        }

        public bool TryUpdateStartInstructionGraphic(string sourceFilePath, out string errorMessage)
        {
            if (!TryUpdateGraphicResource(CustomStartInstructionFilePrefix, sourceFilePath, out errorMessage))
                return false;

            RefreshStartInstructionGraphic();
            return true;
        }

        public bool TryResetStartInstructionGraphic(out string errorMessage)
        {
            if (!TryResetGraphicResource(CustomStartInstructionFilePrefix, out errorMessage))
                return false;

            RefreshStartInstructionGraphic();
            return true;
        }

        public bool TryUpdateWarningInfoGraphic(string sourceFilePath, out string errorMessage)
        {
            if (!TryUpdateGraphicResource(CustomWarningInfoFilePrefix, sourceFilePath, out errorMessage))
                return false;

            RefreshWarningInfoGraphic();
            return true;
        }

        public bool TryResetWarningInfoGraphic(out string errorMessage)
        {
            if (!TryResetGraphicResource(CustomWarningInfoFilePrefix, out errorMessage))
                return false;

            RefreshWarningInfoGraphic();
            return true;
        }
    }
}
