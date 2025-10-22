using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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

        // --- Kameras & Drucker ---
        public ObservableCollection<string> AvailableCameras { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AvailablePrinters { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableUsbDrives { get; } = new ObservableCollection<string>();

        private readonly List<string> _allowedCameraNames = new();
        private readonly List<string> _allowedPrinterNames = new();
        private readonly List<string> _allowedTemplateNames = new();
        private readonly object _usbSyncLock = new();

        public IReadOnlyList<string> AllowedCameraNames => _allowedCameraNames;
        public IReadOnlyList<string> AllowedPrinterNames => _allowedPrinterNames;
        public IReadOnlyList<string> AllowedTemplateNames => _allowedTemplateNames;

        public string AllowedCamerasSummary => BuildAllowedSummary(_allowedCameraNames, AvailableCameras, "Keine Geräte gefunden");
        public string AllowedPrintersSummary => BuildAllowedSummary(_allowedPrinterNames, AvailablePrinters, "Keine Geräte gefunden");
        public string AllowedTemplatesSummary => BuildAllowedSummary(_allowedTemplateNames, Templates.Select(t => t.Name), "Keine Designs gefunden");
        public string CameraRotationSummary => _cameraRotate180 ? "Kamera: 180° gedreht" : "Kamera: normal";
        public double CameraRotationAngle => _cameraRotate180 ? 180d : 0d;
        public string UsbDriveSummary => FormatUsbSummary(_selectedUsbDrivePath);

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
        private TemplateItem _selectedTemplate1;
        public TemplateItem SelectedTemplate1
        {
            get => _selectedTemplate1;
            set
            {
                if (_selectedTemplate1 != value)
                {
                    _selectedTemplate1 = value;
                    OnPropertyChanged();
                    if (ActiveTemplate == null && value != null)
                        ActiveTemplate = value;
                }
            }
        }

        private TemplateItem _selectedTemplate2;
        public TemplateItem SelectedTemplate2
        {
            get => _selectedTemplate2;
            set
            {
                if (!_allowTwoTemplates && value != null)
                {
                    return;
                }

                if (_selectedTemplate2 != value)
                {
                    _selectedTemplate2 = value;
                    OnPropertyChanged();
                    if (ActiveTemplate == null && value != null)
                        ActiveTemplate = value;
                }
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

        private int _previewDurationSeconds = 3;
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
                return "Alle verfügbar";
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
        /// Liefert den Text f�r den Speichern-Button:
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

                    if (ReferenceEquals(_activeTemplate, removed))
                    {
                        ActiveTemplate = _selectedTemplate1;
                    }
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

            // Templates laden
            string templatesRoot = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures),
                "Fotobox", "templates"
            );
            if (Directory.Exists(templatesRoot))
            {
                foreach (var zipFile in Directory.GetFiles(templatesRoot, "*.zip"))
                {
                    BitmapImage previewImg = null;
                    try
                    {
                        using (var archive = ZipFile.OpenRead(zipFile))
                        {
                            var entry = archive.GetEntry("preview.png");
                            if (entry != null)
                            {
                                using (var stream = entry.Open())
                                using (var ms = new MemoryStream())
                                {
                                    stream.CopyTo(ms);
                                    ms.Position = 0;
                                    previewImg = new BitmapImage();
                                    previewImg.BeginInit();
                                    previewImg.StreamSource = ms;
                                    previewImg.CacheOption = BitmapCacheOption.OnLoad;
                                    previewImg.EndInit();
                                    previewImg.Freeze();
                                }
                            }
                        }
                    }
                    catch { }

                    Templates.Add(new TemplateItem
                    {
                        Name = Path.GetFileNameWithoutExtension(zipFile),
                        ZipPath = zipFile,
                        PreviewImage = previewImg
                    });
                }
            }

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





