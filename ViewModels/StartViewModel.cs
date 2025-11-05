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

namespace FotoboxApp.ViewModels
{
    public class StartViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public ObservableCollection<TemplateItem> Templates { get; } = new ObservableCollection<TemplateItem>();

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
        private static readonly string GraphicsVersionMarkerPath = Path.Combine(GraphicsAssetsFolder, "graphics_defaults.version");

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

        private static string GetTemplatesRootPath() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Fotobox", "templates");

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

        private void ReloadTemplatesFromDisk()
        {
            Templates.Clear();

            var templatesRoot = GetTemplatesRootPath();
            if (!Directory.Exists(templatesRoot))
            {
                OnPropertyChanged(nameof(TemplateSlot1Template));
                OnPropertyChanged(nameof(TemplateSlot1Preview));
                OnPropertyChanged(nameof(TemplateSlot2Template));
                OnPropertyChanged(nameof(TemplateSlot2Preview));
                return;
            }

            foreach (var zipFile in Directory.GetFiles(templatesRoot, "*.zip").OrderBy(Path.GetFileName))
            {
            Templates.Add(new TemplateItem
            {
                Name = Path.GetFileNameWithoutExtension(zipFile),
                ZipPath = zipFile,
                PreviewImage = LoadTemplatePreview(zipFile)
                });
            }

            OnPropertyChanged(nameof(TemplateSlot1Template));
            OnPropertyChanged(nameof(TemplateSlot1Preview));
            OnPropertyChanged(nameof(TemplateSlot2Template));
            OnPropertyChanged(nameof(TemplateSlot2Preview));

            EnsureDefaultTemplateValid();
            EnsureDefaultTemplateAllowed();
        }

        public void RefreshTemplatesFromDisk()
        {
            EnsureDefaultGraphicsActive();
            ReloadTemplatesFromDisk();
            NormalizeAllowedTemplates();
            EnsureSelectedTemplatesValid();
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

        public TemplateItem DefaultTemplate
        {
            get => FindTemplateByName(_defaultTemplateName);
            set => UpdateDefaultTemplate(value?.Name, persist: true);
        }

        public string DefaultTemplateName => _defaultTemplateName;

        public bool HasDefaultTemplate => DefaultTemplate != null;

        public string DefaultTemplateDisplayName => DefaultTemplate?.Name ?? "Kein Standard";

        public ImageSource DefaultTemplatePreview => (ImageSource)(DefaultTemplate?.PreviewImage ?? DefaultTemplatePreviewImage);

        public void SetDefaultTemplateByName(string templateName) => UpdateDefaultTemplate(templateName, persist: true);

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
                }
            }
        }

        private string _galleryName = "";
        public string GalleryName
        {
            get => _galleryName;
            set
            {
                if (_galleryName != value)
                {
                    _galleryName = value;
                    OnPropertyChanged();
                    // persist on change
                    try { FotoboxApp.Services.SettingsService.SaveGalleryName(_galleryName); } catch { }
                }
            }
        }

        private const int MaxPostProcessDelaySeconds = 30;

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

        private int _startReadyDelaySeconds = 3;
        public int StartReadyDelaySeconds
        {
            get => _startReadyDelaySeconds;
            set
            {
                var clamped = System.Math.Max(0, value);
                if (_startReadyDelaySeconds == clamped)
                    return;

                _startReadyDelaySeconds = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StartReadyDelayMilliseconds));
                try { SettingsService.SaveStartReadyDelaySeconds(_startReadyDelaySeconds); } catch { }
            }
        }
        public int StartReadyDelayMilliseconds => _startReadyDelaySeconds * 1000;

        private int _collageCreationDelaySeconds = 2;
        public int CollageCreationDelaySeconds
        {
            get => _collageCreationDelaySeconds;
            set
            {
                var clamped = System.Math.Max(0, value);
                if (_collageCreationDelaySeconds == clamped)
                    return;

                _collageCreationDelaySeconds = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CollageCreationDelayMilliseconds));
                try { SettingsService.SaveCollageCreationDelaySeconds(_collageCreationDelaySeconds); } catch { }
            }
        }
        public int CollageCreationDelayMilliseconds => _collageCreationDelaySeconds * 1000;

        private int _postProcessDelaySeconds;
        public int PostProcessDelaySeconds
        {
            get => _postProcessDelaySeconds;
            set
            {
                var clamped = value;
                if (clamped < 0)
                {
                    clamped = 0;
                }
                else if (clamped > MaxPostProcessDelaySeconds)
                {
                    clamped = MaxPostProcessDelaySeconds;
                }
                if (_postProcessDelaySeconds == clamped)
                {
                    return;
                }

                _postProcessDelaySeconds = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PostProcessDelayMilliseconds));

            try { _startReadyDelaySeconds = SettingsService.LoadStartReadyDelaySeconds(); } catch { _startReadyDelaySeconds = 3; }
            OnPropertyChanged(nameof(StartReadyDelaySeconds));
            OnPropertyChanged(nameof(StartReadyDelayMilliseconds));
            try { _collageCreationDelaySeconds = SettingsService.LoadCollageCreationDelaySeconds(); } catch { _collageCreationDelaySeconds = 2; }
            OnPropertyChanged(nameof(CollageCreationDelaySeconds));
            OnPropertyChanged(nameof(CollageCreationDelayMilliseconds));
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
            var result = new TemplateImportResult();

            if (filePaths == null)
            {
                return result;
            }

            var templatesRoot = GetTemplatesRootPath();
            Directory.CreateDirectory(templatesRoot);

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

                    var destPath = Path.Combine(templatesRoot, destName);
                    var sourceFullPath = Path.GetFullPath(filePath);
                    var destFullPath = Path.GetFullPath(destPath);

                    if (string.Equals(sourceFullPath, destFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(destFullPath))
                        {
                            result.UpdatedTemplates.Add(Path.GetFileNameWithoutExtension(destName));
                        }
                        continue;
                    }

                    var wasExisting = File.Exists(destPath);
                    File.Copy(filePath, destPath, true);

                    if (wasExisting)
                    {
                        result.UpdatedTemplates.Add(Path.GetFileNameWithoutExtension(destName));
                    }
                    else
                    {
                        result.ImportedTemplates.Add(Path.GetFileNameWithoutExtension(destName));
                    }
                }
                catch (Exception ex)
                {
                    result.FailedFiles.Add((filePath, ex.Message));
                }
            }

            RefreshTemplatesFromDisk();
            try { SettingsService.SaveAllowedTemplates(_allowedTemplateNames); } catch { }

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
                SelectedTemplate1 = options[0];
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
                var fallback = options.FirstOrDefault(t => SelectedTemplate1 == null || !Matches(t, SelectedTemplate1));
                SelectedTemplate2 = fallback;
            }

            if (ActiveTemplate != null && !options.Any(t => Matches(t, ActiveTemplate)))
            {
                ActiveTemplate = null;
            }

            if (ActiveTemplate == null)
            {
                ActiveTemplate = SelectedTemplate1 ?? SelectedTemplate2;
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

            try { _allowTwoTemplates = SettingsService.LoadAllowTwoTemplates(); } catch { _allowTwoTemplates = false; }
            OnPropertyChanged(nameof(AllowTwoTemplates));

            try { _postProcessDelaySeconds = SettingsService.LoadPostProcessDelaySeconds(); } catch { _postProcessDelaySeconds = 0; }
            OnPropertyChanged(nameof(PostProcessDelaySeconds));
            OnPropertyChanged(nameof(PostProcessDelayMilliseconds));

            try { _allowedCameraNames.AddRange(SettingsService.LoadAllowedCameras()); } catch { }
            try { _allowedPrinterNames.AddRange(SettingsService.LoadAllowedPrinters()); } catch { }
            try { _allowedTemplateNames.AddRange(SettingsService.LoadAllowedTemplates()); } catch { }
            try { _cameraRotate180 = SettingsService.LoadCameraRotate180(); } catch { _cameraRotate180 = false; }
            OnPropertyChanged(nameof(CameraRotate180));
            OnPropertyChanged(nameof(CameraRotationSummary));
            OnPropertyChanged(nameof(CameraRotationAngle));

            try { _allowDirektdruck = SettingsService.LoadAllowDirektdruck(); } catch { _allowDirektdruck = false; }
            OnPropertyChanged(nameof(AllowDirektdruck));
            if (!_allowDirektdruck && _direktdruck)
            {
                _direktdruck = false;
                OnPropertyChanged(nameof(Direktdruck));
                OnPropertyChanged(nameof(SaveButtonLabel));
            }

            try { _allowGalerie = SettingsService.LoadAllowGalerie(); } catch { _allowGalerie = true; }
            OnPropertyChanged(nameof(AllowGalerie));
            if (!_allowGalerie && _galerieButton)
            {
                _galerieButton = false;
                OnPropertyChanged(nameof(GalerieButton));
            }

            try { _allowFotoFilter = SettingsService.LoadAllowFotoFilter(); } catch { _allowFotoFilter = false; }
            OnPropertyChanged(nameof(AllowFotoFilter));
            if (!_allowFotoFilter && _fotoFilter)
            {
                _fotoFilter = false;
                OnPropertyChanged(nameof(FotoFilter));
            }

            EnsureDefaultGraphicsActive();
            ReloadTemplatesFromDisk();
            RefreshStartInstructionGraphic();
            RefreshWarningInfoGraphic();

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
                var preferredCamera = CameraHelper.GetConnectedCameraName();
                SelectedCameraName = !string.IsNullOrWhiteSpace(preferredCamera) && selectableCameras.Contains(preferredCamera)
                    ? preferredCamera
                    : selectableCameras[0];
            }
            else
            {
                SelectedCameraName = "Keine Kamera gefunden";
            }

            var selectablePrinters = GetSelectablePrinters();
            if (selectablePrinters.Count > 0)
            {
                var preferredPrinter = PrinterHelper.GetDefaultPrinterName();
                SelectedPrinterName = !string.IsNullOrWhiteSpace(preferredPrinter) && selectablePrinters.Contains(preferredPrinter)
                    ? preferredPrinter
                    : selectablePrinters[0];
            }
            else
            {
                SelectedPrinterName = "Kein Drucker gefunden";
            }
        }
    }
}





