using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FotoboxApp.Models;
using FotoboxApp.Services;
using FotoboxApp.Utilities;

namespace FotoboxApp.ViewModels
{
    public class StartViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public ObservableCollection<TemplateItem> Templates { get; } = new ObservableCollection<TemplateItem>();
        public ObservableCollection<TemplateItem> DefaultTemplates { get; } = new ObservableCollection<TemplateItem>();
        private StatManager.StatsSnapshot _statsSnapshot = new StatManager.StatsSnapshot();

        public sealed class TemplateImportResult
        {
            public List<string> ImportedTemplates { get; } = new();
            public List<string> UpdatedTemplates { get; } = new();
            public List<string> InvalidFiles { get; } = new();
            public List<(string File, string Error)> FailedFiles { get; } = new();
            public bool HasChanges => ImportedTemplates.Count > 0 || UpdatedTemplates.Count > 0;
        }

        // --- Kameras & Drucker ---
        public ObservableCollection<string> AvailableCameras { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AvailablePrinters { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableUsbDrives { get; } = new ObservableCollection<string>();

        private readonly List<string> _allowedCameraNames = new();
        private readonly List<string> _allowedPrinterNames = new();
        private readonly List<string> _allowedTemplateNames = new();
        private readonly object _usbSyncLock = new();

        private static readonly string GraphicsAssetsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Fotobox");
        private static readonly string[] CustomGraphicExtensions = { ".png", ".jpg", ".jpeg" };

        private static readonly Uri DefaultStartInstructionUri = new("pack://application:,,,/FotoboxApp;component/Assets/start_instructions.png", UriKind.Absolute);
        private static readonly Uri DefaultWarningInfoUri = new("pack://application:,,,/FotoboxApp;component/Assets/warning_hint.png", UriKind.Absolute);
        private static readonly BitmapImage DefaultTemplatePreviewImage = LoadBitmapImage(new Uri("pack://application:,,,/FotoboxApp;component/Assets/template_placeholder.png", UriKind.Absolute));
        private const string CustomStartInstructionFilePrefix = "start_instruction_custom";
        private const string CustomWarningInfoFilePrefix = "warning_hint_custom";
        private const int GraphicsDefaultsVersion = 2025020601;
        private static readonly string GraphicsConfigFolder = AppStorage.EnsureDirectory("graphics");
        private static readonly string GraphicsVersionMarkerPath = Path.Combine(GraphicsConfigFolder, "graphics_defaults.version");
        private static readonly string LegacyGraphicsVersionMarkerPath = Path.Combine(GraphicsAssetsFolder, "graphics_defaults.version");
        private static readonly string TemplatesRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates");
        private static readonly string LegacyTemplatesRootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Fotobox",
            "templates");
        private static bool _templatesLocationChecked;

        private static readonly string DefaultTemplatesRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "default_templates");
        private static readonly string LegacyDefaultTemplatesRootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "Fotobox",
            "default_templates");
        private static bool _defaultTemplatesLocationChecked;
        private static bool _graphicsMarkerMigrated;

        private Brush _startInstructionBrush;
        private string _startInstructionDescription = "Standardgrafik";
        private bool _hasCustomStartInstruction;

        private Brush _warningInfoBrush;
        private string _warningInfoDescription = "Standardgrafik";
        private bool _hasCustomWarningInfo;

        public IReadOnlyList<string> AllowedCameraNames => _allowedCameraNames;
        public IReadOnlyList<string> AllowedPrinterNames => _allowedPrinterNames;
        public IReadOnlyList<string> AllowedTemplateNames => _allowedTemplateNames;

        public string AllowedCamerasSummary => BuildAllowedSummary(_allowedCameraNames, AvailableCameras, "Keine Geräte gefunden");
        public string AllowedPrintersSummary => BuildAllowedSummary(_allowedPrinterNames, AvailablePrinters, "Keine Geräte gefunden");
        public string AllowedTemplatesSummary => BuildAllowedSummary(_allowedTemplateNames, Templates.Select(t => t.Name), "Keine Designs gefunden");
        public string CameraRotationSummary => _cameraRotate180 ? "Kamera: 180° gedreht" : "Kamera: normal";
        public double CameraRotationAngle => _cameraRotate180 ? 180d : 0d;
        public string UsbDriveSummary => FormatUsbSummary(_selectedUsbDrivePath);

        public int TotalSinglePhotos => _statsSnapshot?.TotalSinglePhotos ?? 0;
        public int EventSinglePhotos => _statsSnapshot?.EventSinglePhotos ?? 0;
        public int TotalCollagesCreated => _statsSnapshot?.TotalCollagesCreated ?? 0;
        public int EventCollagesCreated => _statsSnapshot?.EventCollagesCreated ?? 0;
        public int TotalCollagesPrinted => _statsSnapshot?.TotalCollagesPrinted ?? 0;
        public int EventCollagesPrinted => _statsSnapshot?.EventCollagesPrinted ?? 0;
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

        internal static string GetTemplatesRootPath()
        {
            if (!_templatesLocationChecked)
            {
                _templatesLocationChecked = true;
                try
                {
                    if (Directory.Exists(LegacyTemplatesRootPath))
                    {
                        Directory.CreateDirectory(TemplatesRootPath);
                        foreach (var sourceFile in Directory.GetFiles(LegacyTemplatesRootPath, "*.zip"))
                        {
                            try
                            {
                                var destination = Path.Combine(TemplatesRootPath, Path.GetFileName(sourceFile) ?? string.Empty);
                                if (string.IsNullOrWhiteSpace(destination))
                                    continue;

                                if (!File.Exists(destination))
                                {
                                    File.Copy(sourceFile, destination, overwrite: false);
                                }
                            }
                            catch
                            {
                                // Migration best effort; skip problematic files.
                            }
                        }
                    }
                }
                catch
                {
                    // Migration errors are ignored.
                }
            }

            try
            {
                Directory.CreateDirectory(TemplatesRootPath);
            }
            catch
            {
                // If creation fails we still return the path; calling code will handle errors on access.
            }

            return TemplatesRootPath;
        }

        internal static string GetDefaultTemplatesRootPath()
        {
            if (!_defaultTemplatesLocationChecked)
            {
                _defaultTemplatesLocationChecked = true;
                try
                {
                    if (Directory.Exists(LegacyDefaultTemplatesRootPath))
                    {
                        Directory.CreateDirectory(DefaultTemplatesRootPath);
                        foreach (var sourceFile in Directory.GetFiles(LegacyDefaultTemplatesRootPath, "*.zip"))
                        {
                            try
                            {
                                var destination = Path.Combine(DefaultTemplatesRootPath, Path.GetFileName(sourceFile) ?? string.Empty);
                                if (string.IsNullOrWhiteSpace(destination))
                                    continue;

                                if (!File.Exists(destination))
                                {
                                    File.Copy(sourceFile, destination, overwrite: false);
                                }
                            }
                            catch
                            {
                                // Migration best effort; skip problematic files.
                            }
                        }
                    }
                }
                catch
                {
                    // Migration errors are ignored.
                }
            }

            try
            {
                Directory.CreateDirectory(DefaultTemplatesRootPath);
            }
            catch
            {
                // Ignore directory creation issues here; caller will handle errors.
            }

            return DefaultTemplatesRootPath;
        }

        private static BitmapImage LoadTemplatePreview(string zipFile)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipFile);
                var entry = archive.GetEntry("preview.png");
                if (entry == null)
                {
                    return null;
                }

                using var entryStream = entry.Open();
                using var ms = new MemoryStream();
                entryStream.CopyTo(ms);
                ms.Position = 0;

                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = ms;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private static void LoadTemplatesFromFolder(ObservableCollection<TemplateItem> target, string folder)
        {
            target.Clear();

            if (!Directory.Exists(folder))
                return;

            foreach (var zipFile in Directory.GetFiles(folder, "*.zip").OrderBy(Path.GetFileName))
            {
                target.Add(new TemplateItem
                {
                    Name = Path.GetFileNameWithoutExtension(zipFile),
                    ZipPath = zipFile,
                    PreviewImage = LoadTemplatePreview(zipFile)
                });
            }
        }

        private void ReloadTemplatesFromDisk()
        {
            var templatesRoot = GetTemplatesRootPath();
            LoadTemplatesFromFolder(Templates, templatesRoot);

            OnPropertyChanged(nameof(TemplateSlot1Template));
            OnPropertyChanged(nameof(TemplateSlot1Preview));
            OnPropertyChanged(nameof(TemplateSlot2Template));
            OnPropertyChanged(nameof(TemplateSlot2Preview));
        }

        private void ReloadDefaultTemplatesFromDisk()
        {
            var defaultsRoot = GetDefaultTemplatesRootPath();
            LoadTemplatesFromFolder(DefaultTemplates, defaultsRoot);

            if (!string.IsNullOrWhiteSpace(_defaultTemplateName) &&
                DefaultTemplates.All(t => !string.Equals(t.Name, _defaultTemplateName, StringComparison.Ordinal)))
            {
                if (TryMigrateDefaultTemplateFromTemplatesFolder())
                {
                    LoadTemplatesFromFolder(DefaultTemplates, defaultsRoot);
                }
            }

            EnsureDefaultTemplateValid();
            NotifyDefaultTemplateChanged();
        }

        private bool TryMigrateDefaultTemplateFromTemplatesFolder()
        {
            if (string.IsNullOrWhiteSpace(_defaultTemplateName))
                return false;

            var templateFile = _defaultTemplateName + ".zip";
            var sourcePath = Path.Combine(GetTemplatesRootPath(), templateFile);
            if (!File.Exists(sourcePath))
                return false;

            try
            {
                var targetRoot = GetDefaultTemplatesRootPath();
                Directory.CreateDirectory(targetRoot);
                var destination = Path.Combine(targetRoot, templateFile);
                File.Copy(sourcePath, destination, overwrite: true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void RefreshTemplatesFromDisk()
        {
            EnsureDefaultGraphicsActive();
            ReloadTemplatesFromDisk();
            ReloadDefaultTemplatesFromDisk();
            ApplySavedTemplateSelections();
            NormalizeAllowedTemplates();
            EnsureSelectedTemplatesValid();
            RefreshStatistics();
        }

        public void RefreshStatistics()
        {
            try
            {
                _statsSnapshot = StatManager.GetStatsSnapshot(GalleryName);
            }
            catch
            {
                _statsSnapshot = new StatManager.StatsSnapshot();
            }

            OnPropertyChanged(nameof(TotalSinglePhotos));
            OnPropertyChanged(nameof(EventSinglePhotos));
            OnPropertyChanged(nameof(TotalCollagesCreated));
            OnPropertyChanged(nameof(EventCollagesCreated));
            OnPropertyChanged(nameof(TotalCollagesPrinted));
            OnPropertyChanged(nameof(EventCollagesPrinted));
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
                // Ignored: falls back to existing graphics without interrupting startup.
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

        private string _selectedCameraName;
        public string SelectedCameraName
        {
            get => _selectedCameraName;
            set
            {
                if (_selectedCameraName != value)
                {
                    _selectedCameraName = value;
                    OnPropertyChanged();
                    try { SettingsService.SaveSelectedCameraName(value); } catch { }
                }
            }
        }

        private string _selectedPrinterName;
        public string SelectedPrinterName
        {
            get => _selectedPrinterName;
            set
            {
                if (_selectedPrinterName != value)
                {
                    _selectedPrinterName = value;
                    OnPropertyChanged();
                    try { SettingsService.SaveSelectedPrinterName(value); } catch { }
                }
            }
        }

        private string _selectedUsbDrivePath = string.Empty;
        public string SelectedUsbDrivePath
        {
            get => _selectedUsbDrivePath;
            set
            {
                var normalized = NormalizeUsbPath(value);
                if (string.Equals(_selectedUsbDrivePath, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _selectedUsbDrivePath = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UsbDriveSummary));

                try { SettingsService.SaveUsbDrivePath(normalized); } catch { }

                ScheduleUsbSync();
            }
        }

        // --- Template-Auswahl ---
        private string _defaultTemplateName;
        private TemplateItem _selectedTemplate1;
        public TemplateItem SelectedTemplate1
        {
            get => _selectedTemplate1;
            set
            {
                var previous = _selectedTemplate1;

                if (_selectedTemplate1 != value)
                {
                    _selectedTemplate1 = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TemplateSlot1Template));
                    OnPropertyChanged(nameof(TemplateSlot1Preview));
                    OnPropertyChanged(nameof(TemplateSlot2Template));
                    OnPropertyChanged(nameof(TemplateSlot2Preview));
                    UpdateActiveTemplateAfterSelectionChange(previous, value, slotIndex: 1);
                    PersistSelectedTemplates();
                }
            }
        }

        private TemplateItem _selectedTemplate2;
        public TemplateItem SelectedTemplate2
        {
            get => _selectedTemplate2;
            set
            {
                var previous = _selectedTemplate2;

                if (!_allowTwoTemplates && value != null)
                {
                    return;
                }

                if (_selectedTemplate2 != value)
                {
                    _selectedTemplate2 = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TemplateSlot2Template));
                    OnPropertyChanged(nameof(TemplateSlot2Preview));
                    UpdateActiveTemplateAfterSelectionChange(previous, value, slotIndex: 2);
                    PersistSelectedTemplates();
                }
            }
        }

        public TemplateItem TemplateSlot1Template
        {
            get
            {
                if (SelectedTemplate1 != null)
                    return SelectedTemplate1;

                var defaultTemplate = DefaultTemplate;
                if (defaultTemplate != null)
                    return defaultTemplate;

                var options = GetSelectableTemplates();
                return options.FirstOrDefault();
            }
        }

        public TemplateItem TemplateSlot2Template
        {
            get
            {
                if (!_allowTwoTemplates)
                    return null;

                return SelectedTemplate2;
            }
        }

        public ImageSource TemplateSlot1Preview
        {
            get
            {
                var template = TemplateSlot1Template;
                return (ImageSource)(template?.PreviewImage ?? DefaultTemplatePreviewImage);
            }
        }

        public ImageSource TemplateSlot2Preview
        {
            get
            {
                var template = TemplateSlot2Template;
                return (ImageSource)(template?.PreviewImage ?? DefaultTemplatePreviewImage);
            }
        }

        private void UpdateActiveTemplateAfterSelectionChange(TemplateItem previous, TemplateItem current, int slotIndex)
        {
            // If a slot loses its selection and it was active, fall back to the remaining template/default
            if (previous != null && current == null && ReferenceEquals(ActiveTemplate, previous))
            {
                if (slotIndex == 1 && SelectedTemplate2 != null)
                {
                    ActiveTemplate = SelectedTemplate2;
                    return;
                }

                if (slotIndex == 2 && SelectedTemplate1 != null)
                {
                    ActiveTemplate = SelectedTemplate1;
                    return;
                }

                ActiveTemplate = TemplateSlot1Template ?? TemplateSlot2Template;
                return;
            }

            // If only one template is selected, make sure it is marked active
            if (current != null)
            {
                var hasOnlyFirst = SelectedTemplate1 != null && SelectedTemplate2 == null;
                var hasOnlySecond = SelectedTemplate2 != null && SelectedTemplate1 == null;

                if ((hasOnlyFirst && ReferenceEquals(current, SelectedTemplate1)) ||
                    (hasOnlySecond && ReferenceEquals(current, SelectedTemplate2)))
                {
                    ActiveTemplate = current;
                }
            }

            if (ActiveTemplate == null)
            {
                ActiveTemplate = TemplateSlot1Template ?? TemplateSlot2Template;
            }
        }

        private void PersistSelectedTemplates()
        {
            if (_suspendTemplatePersistence)
                return;

            _pendingSelectedTemplate1Name = SelectedTemplate1?.Name ?? string.Empty;
            _pendingSelectedTemplate2Name = SelectedTemplate2?.Name ?? string.Empty;
            var hasManualTemplateSelection = SelectedTemplate1 != null || SelectedTemplate2 != null;
            _pendingActiveTemplateName = hasManualTemplateSelection
                ? ActiveTemplate?.Name ?? string.Empty
                : string.Empty;
            try
            {
                SettingsService.SaveSelectedTemplateNames(
                    _pendingSelectedTemplate1Name,
                    _pendingSelectedTemplate2Name,
                    _pendingActiveTemplateName);
            }
            catch { }
        }

        public TemplateItem DefaultTemplate
        {
            get => FindDefaultTemplateByName(_defaultTemplateName);
            set => UpdateDefaultTemplate(value?.Name, persist: true);
        }

        private string _pendingSelectedTemplate1Name = string.Empty;
        private string _pendingSelectedTemplate2Name = string.Empty;
        private string _pendingActiveTemplateName = string.Empty;
        private bool _suspendTemplatePersistence;

        public string DefaultTemplateName => _defaultTemplateName;

        public bool HasDefaultTemplate => DefaultTemplate != null;

        public string DefaultTemplateDisplayName => DefaultTemplate?.Name ?? "Kein Standard";

        public ImageSource DefaultTemplatePreview => (ImageSource)(DefaultTemplate?.PreviewImage ?? DefaultTemplatePreviewImage);

        public void SetDefaultTemplateByName(string templateName) => UpdateDefaultTemplate(templateName, persist: true);

        private TemplateItem FindDefaultTemplateByName(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                return null;
            }

            return DefaultTemplates.FirstOrDefault(t => string.Equals(t.Name, templateName, StringComparison.Ordinal));
        }

        private TemplateItem FindTemplateByName(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                return null;

            return Templates.FirstOrDefault(t => string.Equals(t.Name, templateName, StringComparison.Ordinal));
        }

        private void UpdateDefaultTemplate(string templateName, bool persist)
        {
            var resolved = FindDefaultTemplateByName(templateName);
            var newName = resolved?.Name ?? string.Empty;

            if (string.Equals(_defaultTemplateName, newName, StringComparison.Ordinal))
            {
                if (persist)
                {
                    try { SettingsService.SaveDefaultTemplateName(_defaultTemplateName); } catch { }
                }
                return;
            }

            _defaultTemplateName = newName;
            NotifyDefaultTemplateChanged();

            if (persist)
            {
                try { SettingsService.SaveDefaultTemplateName(_defaultTemplateName); } catch { }
            }
        }

        private TemplateItem _activeTemplate;
        public TemplateItem ActiveTemplate
        {
            get => _activeTemplate;
            set
            {
                if (_activeTemplate != value)
                {
                    _activeTemplate = value;
                    OnPropertyChanged();
                    PersistSelectedTemplates();
                }
            }
        }

        private string _galleryName = "";
        public string CurrentGalleryDisplayName => string.IsNullOrWhiteSpace(_galleryName) ? "Kein Galerie-Name" : _galleryName;

        public string GalleryName
        {
            get => _galleryName;
            set
            {
                if (_galleryName != value)
                {
                    _galleryName = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentGalleryDisplayName));
                    // persist on change
                    try { FotoboxApp.Services.SettingsService.SaveGalleryName(_galleryName); } catch { }
                    RefreshStatistics();
                }
            }
        }

        private const int MinDelaySeconds = 1;
        private const int MaxDelaySeconds = 10;
        private const int DefaultDelaySeconds = 3;

        private int _previewDurationSeconds = 7;
        public int PreviewDurationSeconds
        {
            get => _previewDurationSeconds;
            set
            {
                if (_previewDurationSeconds != value)
                {
                    _previewDurationSeconds = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PreviewDurationMs));
                }
            }
        }
        public int PreviewDurationMs => _previewDurationSeconds * 1000;

        private int _startReadyDelaySeconds = DefaultDelaySeconds;
        public int StartReadyDelaySeconds
        {
            get => _startReadyDelaySeconds;
            set
            {
                var clamped = ClampDelay(value);
                if (_startReadyDelaySeconds == clamped)
                    return;

                _startReadyDelaySeconds = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StartReadyDelayMilliseconds));
                try { SettingsService.SaveStartReadyDelaySeconds(_startReadyDelaySeconds); } catch { }
            }
        }
        public int StartReadyDelayMilliseconds => _startReadyDelaySeconds * 1000;

        private int _collageCreationDelaySeconds = DefaultDelaySeconds;
        public int CollageCreationDelaySeconds
        {
            get => _collageCreationDelaySeconds;
            set
            {
                var clamped = ClampDelay(value);
                if (_collageCreationDelaySeconds == clamped)
                    return;

                _collageCreationDelaySeconds = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CollageCreationDelayMilliseconds));
                try { SettingsService.SaveCollageCreationDelaySeconds(_collageCreationDelaySeconds); } catch { }
            }
        }
        public int CollageCreationDelayMilliseconds => _collageCreationDelaySeconds * 1000;

        private int _postProcessDelaySeconds = DefaultDelaySeconds;
        public int PostProcessDelaySeconds
        {
            get => _postProcessDelaySeconds;
            set
            {
                var clamped = ClampDelay(value);
                if (_postProcessDelaySeconds == clamped)
                {
                    return;
                }

                _postProcessDelaySeconds = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PostProcessDelayMilliseconds));
                try { SettingsService.SavePostProcessDelaySeconds(clamped); } catch { }
            }
        }
        public int PostProcessDelayMilliseconds => _postProcessDelaySeconds * 1000;

        public IReadOnlyList<string> GetSelectableCameras() => GetSelectableDevices(_allowedCameraNames, AvailableCameras);
        public IReadOnlyList<string> GetSelectablePrinters() => GetSelectableDevices(_allowedPrinterNames, AvailablePrinters);
        public IReadOnlyList<TemplateItem> GetSelectableTemplates()
        {
            if (Templates.Count == 0)
            {
                return Array.Empty<TemplateItem>();
            }

            if (_allowedTemplateNames.Count == 0)
            {
                return Templates.ToList();
            }

            var allowedLookup = new HashSet<string>(_allowedTemplateNames, StringComparer.Ordinal);
            var filtered = Templates.Where(t => allowedLookup.Contains(t.Name)).ToList();
            return filtered.Count > 0 ? filtered : Templates.ToList();
        }

        public void UpdateAllowedCameras(IEnumerable<string> selected)
        {
            ApplyAllowedSelection(_allowedCameraNames, selected, AvailableCameras);
            SettingsService.SaveAllowedCameras(_allowedCameraNames);
            OnPropertyChanged(nameof(AllowedCamerasSummary));
            EnsureSelectedCameraValid();
        }

        public void UpdateAllowedPrinters(IEnumerable<string> selected)
        {
            ApplyAllowedSelection(_allowedPrinterNames, selected, AvailablePrinters);
            SettingsService.SaveAllowedPrinters(_allowedPrinterNames);
            OnPropertyChanged(nameof(AllowedPrintersSummary));
            EnsureSelectedPrinterValid();
        }

        public void UpdateAllowedTemplates(IEnumerable<string> selectedNames)
        {
            ApplyAllowedSelection(_allowedTemplateNames, selectedNames, Templates.Select(t => t.Name));
            SettingsService.SaveAllowedTemplates(_allowedTemplateNames);
            OnPropertyChanged(nameof(AllowedTemplatesSummary));
            EnsureSelectedTemplatesValid();
        }

        public TemplateImportResult ImportTemplatesFromFiles(IEnumerable<string> filePaths)
        {
            var result = ImportTemplatesIntoFolder(filePaths, GetTemplatesRootPath(), autoAllow: true);
            RefreshTemplatesFromDisk();
            try { SettingsService.SaveAllowedTemplates(_allowedTemplateNames); } catch { }
            return result;
        }

        public TemplateImportResult ImportDefaultTemplatesFromFiles(IEnumerable<string> filePaths)
        {
            var result = ImportTemplatesIntoFolder(filePaths, GetDefaultTemplatesRootPath(), autoAllow: false);
            ReloadDefaultTemplatesFromDisk();
            return result;
        }

        private TemplateImportResult ImportTemplatesIntoFolder(IEnumerable<string> filePaths, string targetFolder, bool autoAllow)
        {
            var result = new TemplateImportResult();

            if (filePaths == null)
            {
                return result;
            }

            Directory.CreateDirectory(targetFolder);

            foreach (var filePath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        result.InvalidFiles.Add(filePath);
                    }
                    continue;
                }

                if (!string.Equals(Path.GetExtension(filePath), ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    result.InvalidFiles.Add(filePath);
                    continue;
                }

                try
                {
                    var destName = Path.GetFileName(filePath);
                    if (string.IsNullOrWhiteSpace(destName))
                    {
                        result.InvalidFiles.Add(filePath);
                        continue;
                    }

                    var templateName = Path.GetFileNameWithoutExtension(destName);
                    if (string.IsNullOrWhiteSpace(templateName))
                    {
                        result.InvalidFiles.Add(filePath);
                        continue;
                    }

                    var destPath = Path.Combine(targetFolder, destName);
                    var sourceFullPath = Path.GetFullPath(filePath);
                    var destFullPath = Path.GetFullPath(destPath);

                    if (string.Equals(sourceFullPath, destFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(destFullPath))
                        {
                            result.UpdatedTemplates.Add(templateName);
                            if (autoAllow)
                                EnsureTemplateAllowedByName(templateName);
                        }
                        continue;
                    }

                    var wasExisting = File.Exists(destPath);
                    File.Copy(filePath, destPath, true);

                    if (autoAllow)
                        EnsureTemplateAllowedByName(templateName);

                    if (wasExisting)
                    {
                        result.UpdatedTemplates.Add(templateName);
                    }
                    else
                    {
                        result.ImportedTemplates.Add(templateName);
                    }
                }
                catch (Exception ex)
                {
                    result.FailedFiles.Add((filePath, ex.Message));
                }
            }

            return result;
        }

        public bool TryDeleteTemplate(TemplateItem template, out string errorMessage)
        {
            errorMessage = null;
            if (template == null)
            {
                errorMessage = "Kein Design ausgew├ñhlt.";
                return false;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(template.ZipPath) && File.Exists(template.ZipPath))
                {
                    File.Delete(template.ZipPath);
                }
                else if (string.IsNullOrWhiteSpace(template.ZipPath))
                {
                    errorMessage = "Dateipfad des Designs ist ung├╝ltig.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"L├Âschen fehlgeschlagen: {ex.Message}";
                return false;
            }

            _allowedTemplateNames.RemoveAll(name => string.Equals(name, template.Name, StringComparison.Ordinal));
            RefreshTemplatesFromDisk();
            try { SettingsService.SaveAllowedTemplates(_allowedTemplateNames); } catch { }
            return true;
        }

        public bool TryDeleteDefaultTemplate(TemplateItem template, out string errorMessage)
        {
            errorMessage = null;
            if (template == null)
            {
                errorMessage = "Kein Design ausgewählt.";
                return false;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(template.ZipPath) && File.Exists(template.ZipPath))
                {
                    File.Delete(template.ZipPath);
                }
                else if (string.IsNullOrWhiteSpace(template.ZipPath))
                {
                    errorMessage = "Dateipfad des Designs ist ungültig.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Löschen fehlgeschlagen: {ex.Message}";
                return false;
            }

            ReloadDefaultTemplatesFromDisk();
            return true;
        }

        public void RefreshUsbDrives()
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
                .Select(d => NormalizeUsbPath(d.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();

            AvailableUsbDrives.Clear();
            foreach (var drive in drives)
            {
                AvailableUsbDrives.Add(drive);
            }

            if (!string.IsNullOrEmpty(_selectedUsbDrivePath) &&
                !drives.Any(d => string.Equals(d, _selectedUsbDrivePath, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedUsbDrivePath = string.Empty;
                OnPropertyChanged(nameof(SelectedUsbDrivePath));
                OnPropertyChanged(nameof(UsbDriveSummary));
            }
            else
            {
                OnPropertyChanged(nameof(UsbDriveSummary));
            }

            if (!string.IsNullOrEmpty(_selectedUsbDrivePath))
            {
                ScheduleUsbSync();
            }
        }

        public void ResetEventScopedState()
        {
            SettingsService.ResetEventScopedValues(GalleryName);
            LoadPendingTemplateSelections();
            LoadAdminAndUserSettingsFromStorage();
            SelectedTemplate1 = null;
            SelectedTemplate2 = null;
            ActiveTemplate = null;
            EnsureSelectedTemplatesValid();
            PersistSelectedTemplates();
            RefreshStatistics();
        }

        public void HandleCollageSaved(string sourceFile)
        {
            if (string.IsNullOrWhiteSpace(sourceFile) || string.IsNullOrEmpty(_selectedUsbDrivePath))
            {
                return;
            }

            Task.Run(() =>
            {
                lock (_usbSyncLock)
                {
                    try
                    {
                        var targetDir = GetUsbGalleryDirectory();
                        if (targetDir == null)
                            return;

                        Directory.CreateDirectory(targetDir);
                        CopySingleCollageToUsbUnlocked(sourceFile, targetDir);
                    }
                    catch { }
                }
            });
        }

        private bool _cameraRotate180;
        public bool CameraRotate180
        {
            get => _cameraRotate180;
            set
            {
                if (_cameraRotate180 == value)
                {
                    return;
                }

                _cameraRotate180 = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CameraRotationSummary));
                try { SettingsService.SaveCameraRotate180(value); } catch { }
            }
        }

        private void EnsureSelectedCameraValid()
        {
            var options = GetSelectableCameras();
            if (options.Count == 0)
            {
                SelectedCameraName = "Keine Kamera gefunden";
                return;
            }

            if (!options.Contains(SelectedCameraName))
            {
                SelectedCameraName = options[0];
            }
        }

        private void EnsureSelectedPrinterValid()
        {
            var options = GetSelectablePrinters();
            if (options.Count == 0)
            {
                SelectedPrinterName = "Kein Drucker gefunden";
                return;
            }

            if (!options.Contains(SelectedPrinterName))
            {
                SelectedPrinterName = options[0];
            }
        }

        private void EnsureSelectedTemplatesValid()
        {
            var options = GetSelectableTemplates();
            if (options.Count == 0)
            {
                if (SelectedTemplate1 != null) SelectedTemplate1 = null;
                if (SelectedTemplate2 != null) SelectedTemplate2 = null;
                if (ActiveTemplate != null) ActiveTemplate = null;
                OnPropertyChanged(nameof(TemplateSlot1Template));
                OnPropertyChanged(nameof(TemplateSlot1Preview));
                OnPropertyChanged(nameof(TemplateSlot2Template));
                OnPropertyChanged(nameof(TemplateSlot2Preview));
                return;
            }

            static bool Matches(TemplateItem a, TemplateItem b) =>
                a != null && b != null && string.Equals(a.ZipPath, b.ZipPath, StringComparison.OrdinalIgnoreCase);

            if (SelectedTemplate1 != null && !options.Any(t => Matches(t, SelectedTemplate1)))
            {
                SelectedTemplate1 = FindTemplateByName(_pendingSelectedTemplate1Name) ?? options[0];
            }

            if (!_allowTwoTemplates)
            {
                if (SelectedTemplate2 != null)
                {
                    SelectedTemplate2 = null;
                }
            }
            else if (SelectedTemplate2 != null && !options.Any(t => Matches(t, SelectedTemplate2)))
            {
                var preferred = FindTemplateByName(_pendingSelectedTemplate2Name);
                if (preferred == null || !options.Any(t => Matches(t, preferred)))
                {
                    var fallback = options.FirstOrDefault(t => SelectedTemplate1 == null || !Matches(t, SelectedTemplate1));
                    preferred = fallback;
                }
                SelectedTemplate2 = preferred;
            }

            if (ActiveTemplate != null && !options.Any(t => Matches(t, ActiveTemplate)))
            {
                var preferredActive = FindTemplateByName(_pendingActiveTemplateName);
                if (preferredActive != null && options.Any(t => Matches(t, preferredActive)))
                {
                    ActiveTemplate = preferredActive;
                }
                else
                {
                    ActiveTemplate = null;
                }
            }

            if (ActiveTemplate == null)
            {
                var selectedFallback = SelectedTemplate1 ?? SelectedTemplate2;
                if (selectedFallback != null)
                {
                    ActiveTemplate = selectedFallback;
                }
                else
                {
                    // No manual selection -> rely on default/first allowed template without forcing persistence.
                    var automaticFallback = TemplateSlot1Template ?? TemplateSlot2Template;
                    if (automaticFallback != null)
                    {
                        ActiveTemplate = automaticFallback;
                    }
                }
            }

            OnPropertyChanged(nameof(TemplateSlot1Template));
            OnPropertyChanged(nameof(TemplateSlot1Preview));
            OnPropertyChanged(nameof(TemplateSlot2Template));
            OnPropertyChanged(nameof(TemplateSlot2Preview));
        }

        private static string BuildAllowedSummary(IReadOnlyCollection<string> allowed, IEnumerable<string> availableEnumerable, string emptyMessage)
        {
            var availableList = availableEnumerable?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>();

            if (availableList.Count == 0)
            {
                return emptyMessage;
            }

            var availableLookup = new HashSet<string>(availableList, StringComparer.Ordinal);
            var allowedList = allowed?
                .Where(availableLookup.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>();

            var allowedCount = allowedList.Count;
            if (allowedCount == 0 || allowedCount >= availableList.Count && !availableList.Except(allowedList).Any())
            {
                return "Alle verf├╝gbar";
            }

            const int maxItemsToShow = 3;
            if (allowedCount <= maxItemsToShow)
            {
                return string.Join(", ", allowedList);
            }

            var displayItems = allowedList.Take(maxItemsToShow);
            return $"{string.Join(", ", displayItems)} +{allowedCount - maxItemsToShow} weitere";
        }

        private static void NormalizeAllowedDevices(List<string> target, IEnumerable<string> availableEnumerable)
        {
            if (target == null)
            {
                return;
            }

            var availableList = availableEnumerable?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>();

            var selectedLookup = new HashSet<string>(target, StringComparer.Ordinal);
            var normalized = availableList.Where(selectedLookup.Contains).ToList();

            if (normalized.Count == 0 && availableList.Count > 0)
            {
                normalized = availableList;
            }

            target.Clear();
            target.AddRange(normalized);
        }

        private static void ApplyAllowedSelection(List<string> target, IEnumerable<string> selected, IEnumerable<string> availableEnumerable)
        {
            target.Clear();
            var availableList = availableEnumerable?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>();

            if (selected != null)
            {
                var requested = new HashSet<string>(
                    selected.Where(s => !string.IsNullOrWhiteSpace(s)),
                    StringComparer.Ordinal);

                foreach (var item in availableList)
                {
                    if (requested.Contains(item))
                    {
                        target.Add(item);
                    }
                }
            }

            if (target.Count == 0 && availableList.Count > 0)
            {
                target.AddRange(availableList);
            }
        }

        private static IReadOnlyList<string> GetSelectableDevices(List<string> target, IEnumerable<string> availableEnumerable)
        {
            var availableList = availableEnumerable?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>();

            if (availableList.Count == 0)
            {
                return Array.Empty<string>();
            }

            var effective = target.Count > 0
                ? target.Where(name => availableList.Contains(name)).ToList()
                : availableList;

            return effective.Count > 0 ? effective : availableList;
        }

        private void NormalizeAllowedTemplates()
        {
            NormalizeAllowedDevices(_allowedTemplateNames, Templates.Select(t => t.Name));
            OnPropertyChanged(nameof(AllowedTemplatesSummary));
        }

        private void NotifyDefaultTemplateChanged()
        {
            OnPropertyChanged(nameof(DefaultTemplate));
            OnPropertyChanged(nameof(DefaultTemplateName));
            OnPropertyChanged(nameof(HasDefaultTemplate));
            OnPropertyChanged(nameof(DefaultTemplateDisplayName));
            OnPropertyChanged(nameof(DefaultTemplatePreview));
        }

        private void EnsureDefaultTemplateValid()
        {
            if (!string.IsNullOrWhiteSpace(_defaultTemplateName) &&
                DefaultTemplates.Any(t => string.Equals(t.Name, _defaultTemplateName, StringComparison.Ordinal)))
            {
                return;
            }

            var fallback = DefaultTemplates.FirstOrDefault();

            if (fallback != null)
            {
                UpdateDefaultTemplate(fallback.Name, persist: false);
            }
            else if (!string.IsNullOrWhiteSpace(_defaultTemplateName))
            {
                UpdateDefaultTemplate(null, persist: false);
            }
        }

        private void EnsureTemplateAllowedByName(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                return;
            }

            if (_allowedTemplateNames.Any(name => string.Equals(name, templateName, StringComparison.Ordinal)))
            {
                return;
            }

            _allowedTemplateNames.Add(templateName);
            OnPropertyChanged(nameof(AllowedTemplatesSummary));
        }

        private static string NormalizeUsbPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            var trimmed = path.Trim();
            if (!trimmed.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                trimmed += Path.DirectorySeparatorChar;
            }

            return trimmed;
        }

        private static string FormatUsbSummary(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "Kein USB Speicher";

            var normalized = NormalizeUsbPath(path);
            try
            {
                var drive = DriveInfo.GetDrives()
                    .FirstOrDefault(d => string.Equals(NormalizeUsbPath(d.Name), normalized, StringComparison.OrdinalIgnoreCase));

                if (drive != null && drive.IsReady)
                {
                    var label = drive.VolumeLabel;
                    return string.IsNullOrWhiteSpace(label)
                        ? drive.Name
                        : $"{drive.Name.TrimEnd(Path.DirectorySeparatorChar)} ({label})";
                }
            }
            catch { }

            return normalized;
        }

        private void ScheduleUsbSync()
        {
            if (string.IsNullOrEmpty(_selectedUsbDrivePath) || string.IsNullOrWhiteSpace(_galleryName))
            {
                return;
            }

            Task.Run(CopyExistingCollagesToUsb);
        }

        private void CopyExistingCollagesToUsb()
        {
            lock (_usbSyncLock)
            {
                try
                {
                    var sourceDir = GetGalleryDirectory();
                    if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
                        return;

                    var targetDir = GetUsbGalleryDirectory();
                    if (targetDir == null)
                        return;

                    Directory.CreateDirectory(targetDir);

                    foreach (var file in Directory.GetFiles(sourceDir, "*.jpg", SearchOption.TopDirectoryOnly))
                    {
                        CopySingleCollageToUsbUnlocked(file, targetDir);
                    }
                }
                catch { }
            }
        }

        private void CopySingleCollageToUsbUnlocked(string sourceFile, string targetDir)
        {
            if (string.IsNullOrWhiteSpace(sourceFile) || string.IsNullOrWhiteSpace(targetDir))
                return;

            try
            {
                var fileName = Path.GetFileName(sourceFile);
                if (string.IsNullOrEmpty(fileName))
                    return;

                var destination = Path.Combine(targetDir, fileName);
                File.Copy(sourceFile, destination, true);
            }
            catch { }
        }

        private string GetGalleryDirectory()
        {
            var gallery = _galleryName ?? string.Empty;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "Fotobox",
                gallery);
        }

        private static int ClampDelay(int value)
        {
            if (value < MinDelaySeconds)
                return MinDelaySeconds;
            if (value > MaxDelaySeconds)
                return MaxDelaySeconds;
            return value;
        }

        private static int NormalizeDelayOrDefault(int value)
        {
            if (value < MinDelaySeconds)
                return DefaultDelaySeconds;
            return ClampDelay(value);
        }

        private void LoadAdminAndUserSettingsFromStorage()
        {
            try { _allowTwoTemplates = SettingsService.LoadAllowTwoTemplates(); } catch { _allowTwoTemplates = false; }
            if (!_allowTwoTemplates && !string.IsNullOrWhiteSpace(_pendingSelectedTemplate2Name))
            {
                _allowTwoTemplates = true;
            }
            OnPropertyChanged(nameof(AllowTwoTemplates));

            try { _postProcessDelaySeconds = NormalizeDelayOrDefault(SettingsService.LoadPostProcessDelaySeconds()); } catch { _postProcessDelaySeconds = DefaultDelaySeconds; }
            OnPropertyChanged(nameof(PostProcessDelaySeconds));
            OnPropertyChanged(nameof(PostProcessDelayMilliseconds));

            try { _startReadyDelaySeconds = NormalizeDelayOrDefault(SettingsService.LoadStartReadyDelaySeconds()); } catch { _startReadyDelaySeconds = DefaultDelaySeconds; }
            OnPropertyChanged(nameof(StartReadyDelaySeconds));
            OnPropertyChanged(nameof(StartReadyDelayMilliseconds));

            try { _collageCreationDelaySeconds = NormalizeDelayOrDefault(SettingsService.LoadCollageCreationDelaySeconds()); } catch { _collageCreationDelaySeconds = DefaultDelaySeconds; }
            OnPropertyChanged(nameof(CollageCreationDelaySeconds));
            OnPropertyChanged(nameof(CollageCreationDelayMilliseconds));

            try { _allowDirektdruck = SettingsService.LoadAllowDirektdruck(); } catch { _allowDirektdruck = false; }
            OnPropertyChanged(nameof(AllowDirektdruck));

            try { _allowGalerie = SettingsService.LoadAllowGalerie(); } catch { _allowGalerie = true; }
            OnPropertyChanged(nameof(AllowGalerie));

            try { _allowFotoFilter = SettingsService.LoadAllowFotoFilter(); } catch { _allowFotoFilter = false; }
            OnPropertyChanged(nameof(AllowFotoFilter));

            try { _direktdruck = SettingsService.LoadDirektdruckState(); } catch { _direktdruck = true; }
            if (!_allowDirektdruck && _direktdruck)
                _direktdruck = false;
            OnPropertyChanged(nameof(Direktdruck));
            OnPropertyChanged(nameof(SaveButtonLabel));

            try { _galerieButton = SettingsService.LoadGalerieButtonState(); } catch { _galerieButton = true; }
            if (!_allowGalerie && _galerieButton)
                _galerieButton = false;
            OnPropertyChanged(nameof(GalerieButton));

            try { _fotoFilter = SettingsService.LoadFotoFilterState(); } catch { _fotoFilter = true; }
            if (!_allowFotoFilter && _fotoFilter)
                _fotoFilter = false;
            OnPropertyChanged(nameof(FotoFilter));
        }

        private void LoadPendingTemplateSelections()
        {
            try
            {
                var (slot1, slot2, active) = SettingsService.LoadSelectedTemplateNames();
                _pendingSelectedTemplate1Name = slot1 ?? string.Empty;
                _pendingSelectedTemplate2Name = slot2 ?? string.Empty;
                _pendingActiveTemplateName = active ?? string.Empty;
            }
            catch
            {
                _pendingSelectedTemplate1Name = string.Empty;
                _pendingSelectedTemplate2Name = string.Empty;
                _pendingActiveTemplateName = string.Empty;
            }
        }

        private void ApplySavedTemplateSelections()
        {
            var wasSuspended = _suspendTemplatePersistence;
            _suspendTemplatePersistence = true;
            try
            {
                if (!string.IsNullOrWhiteSpace(_pendingSelectedTemplate1Name))
                    EnsureTemplateAllowedByName(_pendingSelectedTemplate1Name);
                if (!string.IsNullOrWhiteSpace(_pendingSelectedTemplate2Name))
                    EnsureTemplateAllowedByName(_pendingSelectedTemplate2Name);
                if (!string.IsNullOrWhiteSpace(_pendingActiveTemplateName))
                    EnsureTemplateAllowedByName(_pendingActiveTemplateName);

                if (!string.IsNullOrWhiteSpace(_pendingSelectedTemplate1Name))
                {
                    var template = FindTemplateByName(_pendingSelectedTemplate1Name);
                    if (template != null)
                        SelectedTemplate1 = template;
                }

                if (!string.IsNullOrWhiteSpace(_pendingSelectedTemplate2Name))
                {
                    var template = FindTemplateByName(_pendingSelectedTemplate2Name);
                    if (template != null)
                        SelectedTemplate2 = template;
                }

                if (!string.IsNullOrWhiteSpace(_pendingActiveTemplateName))
                {
                    var template = FindTemplateByName(_pendingActiveTemplateName);
                    if (template != null)
                        ActiveTemplate = template;
                }
            }
            finally
            {
                _suspendTemplatePersistence = wasSuspended;
            }

            PersistSelectedTemplates();
        }

        private string GetUsbGalleryDirectory()
        {
            if (string.IsNullOrEmpty(_selectedUsbDrivePath))
                return null;

            var gallery = _galleryName;
            if (string.IsNullOrWhiteSpace(gallery))
                return null;

            return Path.Combine(_selectedUsbDrivePath, "Fotobox", gallery);
        }

        private bool _allowDirektdruck;
        public bool AllowDirektdruck
        {
            get => _allowDirektdruck;
            set
            {
                if (_allowDirektdruck == value)
                {
                    return;
                }

                var previouslyAllowed = _allowDirektdruck;
                _allowDirektdruck = value;
                OnPropertyChanged();

                if (!value && _direktdruck)
                {
                    Direktdruck = false;
                }
                else if (value && !previouslyAllowed && !_direktdruck)
                {
                    Direktdruck = true;
                }

                try { SettingsService.SaveAllowDirektdruck(value); } catch { }
            }
        }

        private bool _direktdruck = true;
        public bool Direktdruck
        {
            get => _direktdruck;
            set
            {
                if (!_allowDirektdruck && value)
                {
                    return;
                }

                if (_direktdruck != value)
                {
                    _direktdruck = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SaveButtonLabel));
                    try { SettingsService.SaveDirektdruckState(_direktdruck); } catch { }
                }
            }
        }

        /// <summary>
        /// Liefert den Text f´┐¢r den Speichern-Button:
        /// "NUR SPEICHERN" wenn Direktdruck aktiv ist, sonst "SPEICHERN".
        /// </summary>
        public string SaveButtonLabel => Direktdruck ? "NUR SPEICHERN" : "SPEICHERN";

        private bool _allowGalerie = true;
        public bool AllowGalerie
        {
            get => _allowGalerie;
            set
            {
                if (_allowGalerie == value)
                {
                    return;
                }

                var previouslyAllowed = _allowGalerie;
                _allowGalerie = value;
                OnPropertyChanged();

                if (!value && _galerieButton)
                {
                    GalerieButton = false;
                }
                else if (value && !previouslyAllowed && !_galerieButton)
                {
                    GalerieButton = true;
                }

                try { SettingsService.SaveAllowGalerie(value); } catch { }
            }
        }

        private bool _galerieButton = true;
        public bool GalerieButton
        {
            get => _galerieButton;
            set
            {
                if (!_allowGalerie && value)
                {
                    return;
                }

                if (_galerieButton != value)
                {
                    _galerieButton = value;
                    OnPropertyChanged();
                    try { SettingsService.SaveGalerieButtonState(_galerieButton); } catch { }
                }
            }
        }

        private bool _allowFotoFilter;
        public bool AllowFotoFilter
        {
            get => _allowFotoFilter;
            set
            {
                if (_allowFotoFilter == value)
                {
                    return;
                }

                _allowFotoFilter = value;
                OnPropertyChanged();

                if (!value && _fotoFilter)
                {
                    FotoFilter = false;
                }

                try { SettingsService.SaveAllowFotoFilter(value); } catch { }
            }
        }

        private bool _fotoFilter = true;
        public bool FotoFilter
        {
            get => _fotoFilter;
            set
            {
                if (!_allowFotoFilter && value)
                {
                    return;
                }

                if (_fotoFilter != value)
                {
                    _fotoFilter = value;
                    OnPropertyChanged();
                    try { SettingsService.SaveFotoFilterState(_fotoFilter); } catch { }
                }
            }
        }

        private bool _allowTwoTemplates;
        public bool AllowTwoTemplates
        {
            get => _allowTwoTemplates;
            set
            {
                if (_allowTwoTemplates == value)
                {
                    return;
                }

                _allowTwoTemplates = value;
                OnPropertyChanged();

                if (!_allowTwoTemplates && _selectedTemplate2 != null)
                {
                    var removed = _selectedTemplate2;
                    _selectedTemplate2 = null;
                    OnPropertyChanged(nameof(SelectedTemplate2));
                    OnPropertyChanged(nameof(TemplateSlot2Template));
                    OnPropertyChanged(nameof(TemplateSlot2Preview));

                    if (ReferenceEquals(_activeTemplate, removed))
                    {
                        ActiveTemplate = _selectedTemplate1;
                    }
                }
                else
                {
                    OnPropertyChanged(nameof(TemplateSlot2Template));
                    OnPropertyChanged(nameof(TemplateSlot2Preview));
                }

                try { SettingsService.SaveAllowTwoTemplates(_allowTwoTemplates); } catch { }
            }
        }

        // --- Konstruktor ---
        public StartViewModel()
        {
            // Galerie-Name aus Einstellungen laden
            try { _galleryName = SettingsService.LoadGalleryName() ?? ""; } catch { }
            OnPropertyChanged(nameof(GalleryName));
            OnPropertyChanged(nameof(CurrentGalleryDisplayName));

            try { SettingsService.EnsureEventScopeForGallery(_galleryName); } catch { }
            try { _selectedCameraName = SettingsService.LoadSelectedCameraName(); } catch { _selectedCameraName = null; }
            try { _selectedPrinterName = SettingsService.LoadSelectedPrinterName(); } catch { _selectedPrinterName = null; }
            LoadPendingTemplateSelections();
            LoadAdminAndUserSettingsFromStorage();

            try { _allowedCameraNames.AddRange(SettingsService.LoadAllowedCameras()); } catch { }
            try { _allowedPrinterNames.AddRange(SettingsService.LoadAllowedPrinters()); } catch { }
            try { _allowedTemplateNames.AddRange(SettingsService.LoadAllowedTemplates()); } catch { }
            try { _defaultTemplateName = SettingsService.LoadDefaultTemplateName() ?? string.Empty; } catch { _defaultTemplateName = string.Empty; }
            NotifyDefaultTemplateChanged();
            try { _cameraRotate180 = SettingsService.LoadCameraRotate180(); } catch { _cameraRotate180 = false; }
            OnPropertyChanged(nameof(CameraRotate180));
            OnPropertyChanged(nameof(CameraRotationSummary));
            OnPropertyChanged(nameof(CameraRotationAngle));

            EnsureDefaultGraphicsActive();
            ReloadTemplatesFromDisk();
            ReloadDefaultTemplatesFromDisk();
            ApplySavedTemplateSelections();
            RefreshStartInstructionGraphic();
            RefreshWarningInfoGraphic();
            RefreshStatistics();

            // --- Kamera- & Druckerliste laden ---
            foreach (var cam in CameraHelper.GetAllCameraNames())
                AvailableCameras.Add(cam);
            foreach (var drucker in PrinterHelper.GetAllPrinterNames())
                AvailablePrinters.Add(drucker);

            RefreshUsbDrives();

            NormalizeAllowedDevices(_allowedCameraNames, AvailableCameras);
            NormalizeAllowedDevices(_allowedPrinterNames, AvailablePrinters);
            NormalizeAllowedTemplates();
            OnPropertyChanged(nameof(AllowedCamerasSummary));
            OnPropertyChanged(nameof(AllowedPrintersSummary));
            EnsureSelectedTemplatesValid();

            // --- Standardauswahl setzen ---
            var selectableCameras = GetSelectableCameras();
            if (selectableCameras.Count > 0)
            {
                if (!string.IsNullOrWhiteSpace(_selectedCameraName) && selectableCameras.Contains(_selectedCameraName))
                {
                    SelectedCameraName = _selectedCameraName;
                }
                else
                {
                    var preferredCamera = CameraHelper.GetConnectedCameraName();
                    SelectedCameraName = !string.IsNullOrWhiteSpace(preferredCamera) && selectableCameras.Contains(preferredCamera)
                        ? preferredCamera
                        : selectableCameras[0];
                }
            }
            else
            {
                SelectedCameraName = "Keine Kamera gefunden";
            }

            var selectablePrinters = GetSelectablePrinters();
            if (selectablePrinters.Count > 0)
            {
                if (!string.IsNullOrWhiteSpace(_selectedPrinterName) && selectablePrinters.Contains(_selectedPrinterName))
                {
                    SelectedPrinterName = _selectedPrinterName;
                }
                else
                {
                    var preferredPrinter = PrinterHelper.GetDefaultPrinterName();
                    SelectedPrinterName = !string.IsNullOrWhiteSpace(preferredPrinter) && selectablePrinters.Contains(preferredPrinter)
                        ? preferredPrinter
                        : selectablePrinters[0];
                }
            }
            else
            {
                SelectedPrinterName = "Kein Drucker gefunden";
            }
        }
    }
}
