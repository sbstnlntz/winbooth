// Primary StartViewModel partial defining hardware monitoring, USB sync, state exposure, and UI commands.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using winbooth.Models;
using winbooth.Services;
using winbooth.Utilities;

namespace winbooth.ViewModels
{
    public partial class StartViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public ObservableCollection<TemplateItem> Templates { get; } = new ObservableCollection<TemplateItem>();
        public ObservableCollection<TemplateItem> DefaultTemplates { get; } = new ObservableCollection<TemplateItem>();
        private StatManager.StatsSnapshot _statsSnapshot = new StatManager.StatsSnapshot();

        // --- Cameras & printers ---
        public ObservableCollection<string> AvailableCameras { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AvailablePrinters { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AvailableUsbDrives { get; } = new ObservableCollection<string>();

        private readonly CancellationTokenSource _backgroundCts = new();
        private readonly SemaphoreSlim _usbRefreshSemaphore = new(1, 1);
        private readonly SemaphoreSlim _templateReloadSemaphore = new(1, 1);
        private readonly PriorityQueue<UsbJob, int> _usbJobQueue = new();
        private readonly SemaphoreSlim _usbJobSemaphore = new(0);
        private readonly object _usbJobQueueLock = new();
        private readonly ConcurrentDictionary<string, TemplatePreviewCacheEntry> _templatePreviewCache =
            new(StringComparer.OrdinalIgnoreCase);
        private Task _usbWorkerTask;
        private Task _usbMonitorTask;
        private Task _templateReloadTask = Task.CompletedTask;
        private TimeSpan _usbMonitorInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan UsbMonitorMinInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan UsbMonitorMaxInterval = TimeSpan.FromMinutes(2);
        private DateTime _lastUsbRefreshUtc = DateTime.UtcNow;
        private DateTime _lastCameraFrameUtc = DateTime.UtcNow;
        private bool _isHardwareHealthy = true;
        private string _hardwareHealthMessage = "System bereit";
        private PeriodicTaskHub _periodicTaskHub;
        private bool _usbMonitorStarted;
        private bool _cameraSessionActive;
        private bool _disposed;
        private const int SnapshotFailureAlertThreshold = 3;
        private const int StatsFailureAlertThreshold = 3;
        private const string ShotsFolderName = "shots";

        private static readonly BitmapImage DefaultTemplatePreviewImage = LoadBitmapImage(new Uri("pack://application:,,,/winbooth;component/Assets/template_placeholder.png", UriKind.Absolute));
        private static readonly TemplateStorageService TemplateStorage = TemplateStorageService.Instance;
        private static readonly TimeSpan CameraHeartbeatTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan UsbHeartbeatTimeout = TimeSpan.FromMinutes(2);
        private static Dispatcher UiDispatcher => Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        public string CameraRotationSummary => _cameraRotate180 ? "Kamera: 180° gedreht" : "Kamera: normal";
        public double CameraRotationAngle => _cameraRotate180 ? 180d : 0d;
        public string UsbDriveSummary => FormatUsbSummary(_selectedUsbDrivePath);

        public bool IsHardwareHealthy
        {
            get => _isHardwareHealthy;
            private set
            {
                if (_isHardwareHealthy == value)
                    return;

                _isHardwareHealthy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasHardwareWarning));
            }
        }

        public bool HasHardwareWarning => !_isHardwareHealthy;

        public string HardwareHealthMessage
        {
            get => _hardwareHealthMessage;
            private set
            {
                if (string.Equals(_hardwareHealthMessage, value, StringComparison.Ordinal))
                    return;

                _hardwareHealthMessage = value;
                OnPropertyChanged();
            }
        }

        public DateTime LastCameraFrameUtc
        {
            get => _lastCameraFrameUtc;
            private set
            {
                if (_lastCameraFrameUtc == value)
                    return;

                _lastCameraFrameUtc = value;
                OnPropertyChanged();
            }
        }

        public DateTime LastUsbRefreshUtc
        {
            get => _lastUsbRefreshUtc;
            private set
            {
                if (_lastUsbRefreshUtc == value)
                    return;

                _lastUsbRefreshUtc = value;
                OnPropertyChanged();
            }
        }

        public int TotalSinglePhotos => _statsSnapshot?.TotalSinglePhotos ?? 0;
        public int EventSinglePhotos => _statsSnapshot?.EventSinglePhotos ?? 0;
        public int TotalCollagesCreated => _statsSnapshot?.TotalCollagesCreated ?? 0;
        public int EventCollagesCreated => _statsSnapshot?.EventCollagesCreated ?? 0;
        public int TotalCollagesPrinted => _statsSnapshot?.TotalCollagesPrinted ?? 0;
        public int EventCollagesPrinted => _statsSnapshot?.EventCollagesPrinted ?? 0;
        public int TotalEvents => _statsSnapshot?.TotalEvents ?? 0;
        internal static string GetTemplatesRootPath() => TemplateStorage.TemplatesRootPath;

        internal static string GetDefaultTemplatesRootPath() => TemplateStorage.DefaultTemplatesRootPath;

        private sealed record TemplatePreviewCacheEntry(DateTime LastWriteUtc, BitmapImage Image);
        private sealed record UsbJob(string Context, Func<CancellationToken, Task> Callback);
        private enum UsbJobPriority
        {
            High = 0,
            Low = 1
        }

        private BitmapImage LoadTemplatePreview(string zipFile)
        {
            try
            {
                var lastWrite = File.GetLastWriteTimeUtc(zipFile);
                if (_templatePreviewCache.TryGetValue(zipFile, out var cached) &&
                    cached.LastWriteUtc == lastWrite)
                {
                    return cached.Image;
                }

                var preview = ExtractTemplatePreview(zipFile);
                if (preview != null)
                {
                    _templatePreviewCache[zipFile] = new TemplatePreviewCacheEntry(lastWrite, preview);
                }

                return preview;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapImage ExtractTemplatePreview(string zipFile)
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

        private List<TemplateItem> LoadTemplatesFromFolderSnapshot(string folder)
        {
            var result = new List<TemplateItem>();

            if (!Directory.Exists(folder))
                return result;

            foreach (var zipFile in Directory.GetFiles(folder, "*.zip").OrderBy(Path.GetFileName))
            {
                result.Add(new TemplateItem
                {
                    Name = Path.GetFileNameWithoutExtension(zipFile),
                    ZipPath = zipFile,
                    PreviewImage = LoadTemplatePreview(zipFile) ?? DefaultTemplatePreviewImage
                });
            }

            return result;
        }

        private static void ApplyTemplateSnapshot(ObservableCollection<TemplateItem> target, IReadOnlyList<TemplateItem> snapshot)
        {
            target.Clear();
            foreach (var item in snapshot)
            {
                target.Add(item);
            }
        }

        private Task QueueTemplateReloadAsync(bool reloadUserTemplates, bool reloadDefaultTemplates)
        {
            var token = _backgroundCts.Token;

            Task<List<TemplateItem>> ScheduleSnapshotLoad(string rootPath) =>
                Task.Run(() => LoadTemplatesFromFolderSnapshot(rootPath), token);

            _templateReloadTask = Task.Run(async () =>
            {
                await _templateReloadSemaphore.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    List<TemplateItem> userSnapshot = null;
                    List<TemplateItem> defaultSnapshot = null;

                    if (reloadUserTemplates)
                    {
                        userSnapshot = await ScheduleSnapshotLoad(GetTemplatesRootPath()).ConfigureAwait(false);
                    }

                    if (reloadDefaultTemplates)
                    {
                        defaultSnapshot = await ScheduleSnapshotLoad(GetDefaultTemplatesRootPath()).ConfigureAwait(false);

                        if (!string.IsNullOrWhiteSpace(_defaultTemplateName) &&
                            defaultSnapshot.All(t => !string.Equals(t.Name, _defaultTemplateName, StringComparison.Ordinal)))
                        {
                            if (TryMigrateDefaultTemplateFromTemplatesFolder())
                            {
                                defaultSnapshot = await ScheduleSnapshotLoad(GetDefaultTemplatesRootPath()).ConfigureAwait(false);
                            }
                        }
                    }

                    await UiDispatcher.InvokeAsync(() =>
                    {
                        if (userSnapshot != null)
                        {
                            ApplyTemplateSnapshot(Templates, userSnapshot);
                            OnPropertyChanged(nameof(TemplateSlot1Template));
                            OnPropertyChanged(nameof(TemplateSlot1Preview));
                            OnPropertyChanged(nameof(TemplateSlot2Template));
                            OnPropertyChanged(nameof(TemplateSlot2Preview));
                            ApplySavedTemplateSelections();
                            NormalizeAllowedTemplates();
                            EnsureSelectedTemplatesValid();
                            RefreshStatistics();
                        }

                        if (defaultSnapshot != null)
                        {
                            ApplyTemplateSnapshot(DefaultTemplates, defaultSnapshot);
                            EnsureDefaultTemplateValid();
                            NotifyDefaultTemplateChanged();
                            NormalizeAllowedDefaultTemplates();
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    // shutting down
                }
                catch (Exception ex)
                {
                    DiagnosticsLogger.LogError("Templates", "Fehler beim Aktualisieren der Templates", ex);
                }
                finally
                {
                    _templateReloadSemaphore.Release();
                }
            }, token);

            return _templateReloadTask;
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

        private static T SafeLoad<T>(Func<T> loader, T fallback)
        {
            try
            {
                return loader();
            }
            catch
            {
                return fallback;
            }
        }

        public void RefreshTemplatesFromDisk()
        {
            EnsureDefaultGraphicsActive();
            _ = QueueTemplateReloadAsync(reloadUserTemplates: true, reloadDefaultTemplates: true);
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
            OnPropertyChanged(nameof(TotalEvents));
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
                TriggerAutomaticLocalBackup();
            }
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

            EnqueueUsbJob("Galerie-Synchronisation", SyncGalleryToUsb);
        }

        private void SyncGalleryToUsb(CancellationToken token)
        {
            var sourceDir = GetGalleryDirectory();
            if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
                return;

            var targetDir = GetUsbGalleryDirectory();
            if (targetDir == null)
                return;

            MirrorDirectory(sourceDir, targetDir, token);
        }

        private void MirrorDirectory(string sourceDir, string targetDir, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(targetDir))
                return;

            if (IsShotsDirectory(sourceDir))
                return;

            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                token.ThrowIfCancellationRequested();
                var destination = Path.Combine(targetDir, Path.GetFileName(file) ?? string.Empty);
                if (string.IsNullOrEmpty(destination))
                    continue;
                File.Copy(file, destination, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                token.ThrowIfCancellationRequested();
                var name = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(name))
                    continue;
                if (string.Equals(name, ShotsFolderName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var childTarget = Path.Combine(targetDir, name);
                MirrorDirectory(dir, childTarget, token);
            }
        }

        private static bool IsShotsDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                return false;

            var trimmed = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);

            return !string.IsNullOrEmpty(name) &&
                string.Equals(name, ShotsFolderName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsShotRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return false;

            var normalized = relativePath
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            if (string.IsNullOrEmpty(normalized))
                return false;

            if (string.Equals(normalized, ShotsFolderName, StringComparison.OrdinalIgnoreCase))
                return true;

            return normalized.StartsWith($"{ShotsFolderName}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
        }

        private void TriggerAutomaticLocalBackup()
        {
            var gallery = _galleryName?.Trim();
            if (string.IsNullOrWhiteSpace(gallery))
                return;

            EnqueueUsbJob("Backup-Erstellung", token =>
            {
                token.ThrowIfCancellationRequested();
                BackupService.CreateBackup(gallery);
            }, UsbJobPriority.Low);
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

        private void LoadAdminAndUserSettingsFromStorage(SettingsService.SettingsSnapshot snapshot = null)
        {
            SettingsService.SettingsSnapshot currentSnapshot = snapshot;
            if (currentSnapshot == null)
            {
                try { currentSnapshot = SettingsService.CaptureSnapshot(); } catch { }
            }

            T SafeInvoke<T>(Func<T> loader, T fallback)
            {
                try { return loader(); }
                catch { return fallback; }
            }

            bool ResolveBool(Func<SettingsService.SettingsSnapshot, bool> selector, Func<bool> loader, bool fallback) =>
                currentSnapshot != null ? selector(currentSnapshot) : SafeInvoke(loader, fallback);

            int ResolveInt(Func<SettingsService.SettingsSnapshot, int> selector, Func<int> loader, int fallback) =>
                currentSnapshot != null ? selector(currentSnapshot) : SafeInvoke(loader, fallback);

            _allowTwoTemplates = ResolveBool(s => s.AllowTwoTemplates, SettingsService.LoadAllowTwoTemplates, false);
            if (!_allowTwoTemplates && !string.IsNullOrWhiteSpace(_pendingSelectedTemplate2Name))
            {
                _allowTwoTemplates = true;
            }
            OnPropertyChanged(nameof(AllowTwoTemplates));

            _postProcessDelaySeconds = NormalizeDelayOrDefault(
                ResolveInt(s => s.PostProcessDelaySeconds, SettingsService.LoadPostProcessDelaySeconds, DefaultDelaySeconds));
            OnPropertyChanged(nameof(PostProcessDelaySeconds));
            OnPropertyChanged(nameof(PostProcessDelayMilliseconds));

            _startReadyDelaySeconds = NormalizeDelayOrDefault(
                ResolveInt(s => s.StartReadyDelaySeconds, SettingsService.LoadStartReadyDelaySeconds, DefaultDelaySeconds));
            OnPropertyChanged(nameof(StartReadyDelaySeconds));
            OnPropertyChanged(nameof(StartReadyDelayMilliseconds));

            _collageCreationDelaySeconds = NormalizeDelayOrDefault(
                ResolveInt(s => s.CollageCreationDelaySeconds, SettingsService.LoadCollageCreationDelaySeconds, DefaultDelaySeconds));
            OnPropertyChanged(nameof(CollageCreationDelaySeconds));
            OnPropertyChanged(nameof(CollageCreationDelayMilliseconds));

            _allowDirektdruck = ResolveBool(s => s.AllowDirektdruck, SettingsService.LoadAllowDirektdruck, false);
            OnPropertyChanged(nameof(AllowDirektdruck));

            _allowGalerie = ResolveBool(s => s.AllowGalerie, SettingsService.LoadAllowGalerie, true);
            OnPropertyChanged(nameof(AllowGalerie));

            _allowFotoFilter = ResolveBool(s => s.AllowFotoFilter, SettingsService.LoadAllowFotoFilter, false);
            OnPropertyChanged(nameof(AllowFotoFilter));

            _direktdruck = ResolveBool(s => s.DirektdruckState, SettingsService.LoadDirektdruckState, true);
            if (!_allowDirektdruck && _direktdruck)
                _direktdruck = false;
            OnPropertyChanged(nameof(Direktdruck));
            OnPropertyChanged(nameof(SaveButtonLabel));

            _galerieButton = ResolveBool(s => s.GalerieButtonState, SettingsService.LoadGalerieButtonState, true);
            if (!_allowGalerie && _galerieButton)
                _galerieButton = false;
            OnPropertyChanged(nameof(GalerieButton));

            _fotoFilter = ResolveBool(s => s.FotoFilterState, SettingsService.LoadFotoFilterState, true);
            if (!_allowFotoFilter && _fotoFilter)
                _fotoFilter = false;
            OnPropertyChanged(nameof(FotoFilter));
        }

        private void LoadPendingTemplateSelections(SettingsService.SettingsSnapshot snapshot = null)
        {
            if (snapshot != null)
            {
                _pendingSelectedTemplate1Name = snapshot.SelectedTemplate1Name ?? string.Empty;
                _pendingSelectedTemplate2Name = snapshot.SelectedTemplate2Name ?? string.Empty;
                _pendingActiveTemplateName = snapshot.ActiveTemplateName ?? string.Empty;
                return;
            }

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
        /// Returns the label shown on the save button:
        /// displays "NUR SPEICHERN" when direct print is active, otherwise "SPEICHERN".
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

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try { _periodicTaskHub?.Dispose(); } catch { }
            _periodicTaskHub = null;

            _backgroundCts.Cancel();

            WaitForTask(_usbWorkerTask);
            WaitForTask(_usbMonitorTask);
            WaitForTask(_templateReloadTask);

            _backgroundCts.Dispose();
            _usbRefreshSemaphore.Dispose();
            _templateReloadSemaphore.Dispose();
            _usbJobSemaphore.Dispose();
        }

        private static void WaitForTask(Task task)
        {
            if (task == null)
                return;

            try
            {
                task.Wait(TimeSpan.FromSeconds(5));
            }
            catch { }
        }

        // --- Constructor ---
        public StartViewModel()
        {
            SettingsService.SettingsSnapshot settingsSnapshot = null;
            try { settingsSnapshot = SettingsService.CaptureSnapshot(); } catch { }

            _galleryName = settingsSnapshot?.GalleryName ?? string.Empty;
            OnPropertyChanged(nameof(GalleryName));
            OnPropertyChanged(nameof(CurrentGalleryDisplayName));

            try { SettingsService.EnsureEventScopeForGallery(_galleryName); } catch { }

            _selectedCameraName = settingsSnapshot?.SelectedCameraName;
            _selectedPrinterName = settingsSnapshot?.SelectedPrinterName;
            LoadPendingTemplateSelections(settingsSnapshot);
            LoadAdminAndUserSettingsFromStorage(settingsSnapshot);

            if (settingsSnapshot?.AllowedCameras?.Count > 0)
                _allowedCameraNames.AddRange(settingsSnapshot.AllowedCameras);
            else
                TryPopulateList(_allowedCameraNames, SettingsService.LoadAllowedCameras);

            if (settingsSnapshot?.AllowedPrinters?.Count > 0)
                _allowedPrinterNames.AddRange(settingsSnapshot.AllowedPrinters);
            else
                TryPopulateList(_allowedPrinterNames, SettingsService.LoadAllowedPrinters);

            if (settingsSnapshot?.AllowedTemplates?.Count > 0)
                _allowedTemplateNames.AddRange(settingsSnapshot.AllowedTemplates);
            else
                TryPopulateList(_allowedTemplateNames, SettingsService.LoadAllowedTemplates);

            bool defaultTemplatesConfigured = false;
            if (settingsSnapshot?.AllowedDefaultTemplates?.Count > 0)
            {
                _allowedDefaultTemplateNames.AddRange(settingsSnapshot.AllowedDefaultTemplates);
                defaultTemplatesConfigured = true;
            }
            else
            {
                TryPopulateList(_allowedDefaultTemplateNames, SettingsService.LoadAllowedDefaultTemplates);
                if (_allowedDefaultTemplateNames.Count > 0)
                    defaultTemplatesConfigured = true;
            }
            _defaultTemplatePermissionsConfigured = defaultTemplatesConfigured;

            _defaultTemplateName = settingsSnapshot?.DefaultTemplateName ?? SafeLoad(() => SettingsService.LoadDefaultTemplateName() ?? string.Empty, string.Empty);
            NotifyDefaultTemplateChanged();

            _cameraRotate180 = settingsSnapshot?.CameraRotate180 ?? SafeLoad(SettingsService.LoadCameraRotate180, false);
            OnPropertyChanged(nameof(CameraRotate180));
            OnPropertyChanged(nameof(CameraRotationSummary));
            OnPropertyChanged(nameof(CameraRotationAngle));

            EnsureDefaultGraphicsActive();
            RefreshTemplatesFromDisk();
            RefreshStartInstructionGraphic();
            RefreshWarningInfoGraphic();
            RefreshStatistics();

            // --- Load camera and printer lists ---
            foreach (var cam in CameraHelper.GetAllCameraNames())
                AvailableCameras.Add(cam);
            foreach (var drucker in PrinterHelper.GetAllPrinterNames())
                AvailablePrinters.Add(drucker);

            TriggerUsbRefresh(resetBackoff: true);
            StartUsbMonitorLoop();
            StartUsbWorkQueue();
            StartPeriodicTasks();
            EvaluateHardwareHealth();
            NormalizeAllowedDevices(_allowedCameraNames, AvailableCameras);
            NormalizeAllowedDevices(_allowedPrinterNames, AvailablePrinters);
            OnPropertyChanged(nameof(AllowedCamerasSummary));
            OnPropertyChanged(nameof(AllowedPrintersSummary));
            EnsureSelectedTemplatesValid();

            // --- Apply default selection ---
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







