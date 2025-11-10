// Contains StartViewModel logic for template catalogs, slot selection, previews, and persistence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using winbooth.Models;
using winbooth.Services;
using winbooth.Utilities;

namespace winbooth.ViewModels
{
    public partial class StartViewModel
    {
        // --- Template selection ---
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
                    try { winbooth.Services.SettingsService.SaveGalleryName(_galleryName); } catch { }
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

        public void UpdateAllowedDefaultTemplates(IEnumerable<string> selectedNames)
        {
            ApplyAllowedSelection(_allowedDefaultTemplateNames, selectedNames, DefaultTemplates.Select(t => t.Name));
            _defaultTemplatePermissionsConfigured = true;
            try { SettingsService.SaveAllowedDefaultTemplates(_allowedDefaultTemplateNames); } catch { }
        }

        public void SetDefaultTemplateAvailability(string templateName, bool isEnabled)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                return;
            }

            _defaultTemplatePermissionsConfigured = true;

            if (_allowedDefaultTemplateNames.Count == 0 && DefaultTemplates.Count > 0)
            {
                _allowedDefaultTemplateNames.AddRange(DefaultTemplates.Select(t => t.Name));
            }

            var changed = false;
            if (isEnabled)
            {
                if (!_allowedDefaultTemplateNames.Any(name => string.Equals(name, templateName, StringComparison.Ordinal)))
                {
                    _allowedDefaultTemplateNames.Add(templateName);
                    changed = true;
                }
            }
            else
            {
                var removed = _allowedDefaultTemplateNames.RemoveAll(name => string.Equals(name, templateName, StringComparison.Ordinal));
                changed = removed > 0;
            }

            if (changed)
            {
                try { SettingsService.SaveAllowedDefaultTemplates(_allowedDefaultTemplateNames); } catch { }
            }
        }

        public async Task<TemplateImportResult> ImportTemplatesFromFilesAsync(IEnumerable<string> filePaths, CancellationToken token = default)
        {
            var result = await ImportTemplatesInternalAsync(filePaths, GetTemplatesRootPath(), autoAllow: true, token);
            if (result.HasChanges)
            {
                RefreshTemplatesFromDisk();
            }
            return result;
        }

        public async Task<TemplateImportResult> ImportDefaultTemplatesFromFilesAsync(IEnumerable<string> filePaths, CancellationToken token = default)
        {
            var result = await ImportTemplatesInternalAsync(filePaths, GetDefaultTemplatesRootPath(), autoAllow: false, token);
            if (result.HasChanges)
            {
                foreach (var templateName in result.ImportedTemplates.Concat(result.UpdatedTemplates))
                {
                    EnsureDefaultTemplateAllowedByName(templateName);
                }

                _ = QueueTemplateReloadAsync(reloadUserTemplates: false, reloadDefaultTemplates: true);
            }
            return result;
        }

        private async Task<TemplateImportResult> ImportTemplatesInternalAsync(IEnumerable<string> filePaths, string targetFolder, bool autoAllow, CancellationToken token)
        {
            if (filePaths == null)
                return new TemplateImportResult();

            await TemplateStorage.EnsureLegacyMigrationCompletedAsync(token);
            var result = await TemplateStorage.ImportTemplatesAsync(filePaths, targetFolder, token);

            if (result.HasChanges && autoAllow)
            {
                foreach (var templateName in result.ImportedTemplates.Concat(result.UpdatedTemplates))
                {
                    EnsureTemplateAllowedByName(templateName);
                }

                try { SettingsService.SaveAllowedTemplates(_allowedTemplateNames); } catch { }
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

            _allowedDefaultTemplateNames.RemoveAll(name => string.Equals(name, template.Name, StringComparison.Ordinal));
            try { SettingsService.SaveAllowedDefaultTemplates(_allowedDefaultTemplateNames); } catch { }

            _ = QueueTemplateReloadAsync(reloadUserTemplates: false, reloadDefaultTemplates: true);
            return true;
        }

        public void RefreshUsbDrives() => TriggerUsbRefresh(resetBackoff: true);

        private void TriggerUsbRefresh(bool resetBackoff)
        {
            if (resetBackoff)
                _usbMonitorInterval = UsbMonitorMinInterval;

            _ = TryRefreshUsbDrivesAsync(_backgroundCts.Token);
        }

        private async Task<bool> TryRefreshUsbDrivesAsync(CancellationToken token)
        {
            await _usbRefreshSemaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var drives = await Task.Run(ProbeUsbDrives, token).ConfigureAwait(false);
                await UiDispatcher.InvokeAsync(() => ApplyUsbDriveSnapshot(drives));
                LastUsbRefreshUtc = DateTime.UtcNow;
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                HardwareHealthMessage = $"USB: {ex.Message}";
                IsHardwareHealthy = false;
                return false;
            }
            finally
            {
                _usbRefreshSemaphore.Release();
            }
        }

        private static List<string> ProbeUsbDrives()
        {
            return DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
                .Select(d => NormalizeUsbPath(d.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void ApplyUsbDriveSnapshot(IReadOnlyList<string> drives)
        {
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
            else if (drives.Count == 1 && string.IsNullOrEmpty(_selectedUsbDrivePath))
            {
                SelectedUsbDrivePath = drives[0];
            }
            else
            {
                OnPropertyChanged(nameof(UsbDriveSummary));
            }
        }

        private void StartUsbMonitorLoop()
        {
            if (_usbMonitorStarted)
                return;

            _usbMonitorStarted = true;
            _usbMonitorTask = Task.Run(() => MonitorUsbLoopAsync(_backgroundCts.Token));
        }

        private async Task MonitorUsbLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_usbMonitorInterval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var success = await TryRefreshUsbDrivesAsync(token).ConfigureAwait(false);
                if (success)
                {
                    _usbMonitorInterval = UsbMonitorMinInterval;
                }
                else
                {
                    var next = TimeSpan.FromMilliseconds(
                        Math.Min(_usbMonitorInterval.TotalMilliseconds * 2, UsbMonitorMaxInterval.TotalMilliseconds));
                    _usbMonitorInterval = next;
                }
            }
        }

        private void StartUsbWorkQueue()
        {
            if (_usbWorkerTask != null)
                return;

            _usbWorkerTask = Task.Run(() => ProcessUsbJobsAsync(_backgroundCts.Token));
        }

        private async Task ProcessUsbJobsAsync(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    await _usbJobSemaphore.WaitAsync(token).ConfigureAwait(false);

                    UsbJob job;
                    lock (_usbJobQueueLock)
                    {
                        if (_usbJobQueue.Count == 0)
                        {
                            continue;
                        }

                        job = _usbJobQueue.Dequeue();
                    }

                    token.ThrowIfCancellationRequested();
                    await job.Callback(token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // expected during shutdown
            }
        }

        private void EnqueueUsbJob(string context, Action<CancellationToken> work, UsbJobPriority priority = UsbJobPriority.High)
        {
            UsbJob job = new(context, async token =>
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    work(token);
                    await UiDispatcher.InvokeAsync(() =>
                    {
                        LastUsbRefreshUtc = DateTime.UtcNow;
                        EvaluateHardwareHealth();
                    });
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    ReportUsbError($"{context}: {ex.Message}", ex);
                }

                return;
            });

            lock (_usbJobQueueLock)
            {
                _usbJobQueue.Enqueue(job, (int)priority);
            }

            try
            {
                _usbJobSemaphore.Release();
            }
            catch
            {
                // semaphore disposed during shutdown
            }
        }

        private void ReportUsbError(string message, Exception exception = null)
        {
            UiDispatcher.InvokeAsync(() =>
            {
                HardwareHealthMessage = message;
                IsHardwareHealthy = false;
            });
            DiagnosticsLogger.LogError("USB", message, exception);
            if (exception != null)
            {
                Debug.WriteLine($"[winbooth][USB] {message}: {exception}");
            }
        }

        private void StartPeriodicTasks()
        {
            _periodicTaskHub = new PeriodicTaskHub(TimeSpan.FromSeconds(1), _backgroundCts.Token);
            _periodicTaskHub.RegisterTask("hardware-health", TimeSpan.FromSeconds(10), async token =>
            {
                if (token.IsCancellationRequested)
                    return;

                await UiDispatcher.InvokeAsync(EvaluateHardwareHealth);
            });

            _periodicTaskHub.RegisterTask("state-snapshot", TimeSpan.FromMinutes(1), async token =>
            {
                if (token.IsCancellationRequested)
                    return;

                await PersistStateSnapshotAsync();
            });
        }

        private void EvaluateHardwareHealth()
        {
            var now = DateTime.UtcNow;
            var cameraHealthy = !_cameraSessionActive || now - LastCameraFrameUtc <= CameraHeartbeatTimeout;
            var usbHealthy = now - LastUsbRefreshUtc <= UsbHeartbeatTimeout;
            var snapshotFailures = StateSnapshotService.ConsecutiveWriteFailures;
            var statsFailures = StatManager.ConsecutivePersistFailures;

            if (snapshotFailures >= SnapshotFailureAlertThreshold)
            {
                HardwareHealthMessage = $"Statusspeicher gestört ({snapshotFailures})";
                IsHardwareHealthy = false;
                return;
            }

            if (statsFailures >= StatsFailureAlertThreshold)
            {
                HardwareHealthMessage = $"Statistikspeicher gestört ({statsFailures})";
                IsHardwareHealthy = false;
                return;
            }

            if (cameraHealthy && usbHealthy)
            {
                if (!IsHardwareHealthy)
                {
                    HardwareHealthMessage = "System bereit";
                    IsHardwareHealthy = true;
                }
                return;
            }

            if (!cameraHealthy)
            {
                HardwareHealthMessage = "Keine Kameraframes erkannt – Kamera prüfen";
            }
            else if (!usbHealthy)
            {
                HardwareHealthMessage = "USB-Laufwerke nicht erreichbar";
            }

            IsHardwareHealthy = false;
        }

        private async Task PersistStateSnapshotAsync()
        {
            try
            {
                var snapshot = new StartViewStateSnapshot
                {
                    GalleryName = _galleryName ?? string.Empty,
                    SelectedCameraName = _selectedCameraName ?? string.Empty,
                    SelectedPrinterName = _selectedPrinterName ?? string.Empty,
                    TemplateSlot1Name = _selectedTemplate1?.Name ?? string.Empty,
                    TemplateSlot2Name = _selectedTemplate2?.Name ?? string.Empty,
                    ActiveTemplateName = _activeTemplate?.Name ?? string.Empty,
                    IsHardwareHealthy = IsHardwareHealthy,
                    LastCameraFrameUtc = LastCameraFrameUtc,
                    LastUsbRefreshUtc = LastUsbRefreshUtc
                };

                await StateSnapshotService.SaveSnapshotAsync(snapshot, _backgroundCts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Snapshot persistence is best-effort.
            }
        }

        public void ReportCameraHeartbeat()
        {
            LastCameraFrameUtc = DateTime.UtcNow;
        }

        public void ReportCameraFault(string message)
        {
            HardwareHealthMessage = string.IsNullOrWhiteSpace(message)
                ? "Kamera reagiert nicht"
                : message;
            IsHardwareHealthy = false;
        }

        public void ReportCameraRecovery(string message = "System bereit")
        {
            HardwareHealthMessage = message;
            IsHardwareHealthy = true;
            LastCameraFrameUtc = DateTime.UtcNow;
        }

        public void NotifyCameraSessionStarted()
        {
            _cameraSessionActive = true;
            LastCameraFrameUtc = DateTime.UtcNow;
            EvaluateHardwareHealth();
        }

        public void NotifyCameraSessionEnded()
        {
            _cameraSessionActive = false;
            LastCameraFrameUtc = DateTime.UtcNow;
            EvaluateHardwareHealth();
        }

        public void ResetEventScopedState()
        {
            SettingsService.ResetEventScopedValues(GalleryName);
            SettingsService.SettingsSnapshot snapshot = null;
            try { snapshot = SettingsService.CaptureSnapshot(); } catch { }
            LoadPendingTemplateSelections(snapshot);
            LoadAdminAndUserSettingsFromStorage(snapshot);
            ClearManualTemplateSelections();
            EnsureSelectedTemplatesValid();
            RefreshStatistics();
        }

        public void ClearManualTemplateSelections()
        {
            var wasSuspended = _suspendTemplatePersistence;
            _suspendTemplatePersistence = true;
            try
            {
                SelectedTemplate1 = null;
                SelectedTemplate2 = null;
                ActiveTemplate = null;
            }
            finally
            {
                _suspendTemplatePersistence = wasSuspended;
            }

            PersistSelectedTemplates();
        }

        public void HandleGalleryFileSaved(string sourceFile)
        {
            if (string.IsNullOrWhiteSpace(sourceFile) || string.IsNullOrEmpty(_selectedUsbDrivePath))
            {
                return;
            }

            EnqueueUsbJob("Datei-Export auf USB", token =>
            {
                var galleryDir = GetGalleryDirectory();
                var targetDir = GetUsbGalleryDirectory();
                if (targetDir == null || string.IsNullOrWhiteSpace(galleryDir))
                    return;

                CopyFileToUsbUnlocked(sourceFile, galleryDir, targetDir, token);
            });
        }

        private void CopyFileToUsbUnlocked(string sourceFile, string galleryDir, string targetRoot, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(sourceFile) ||
                string.IsNullOrWhiteSpace(galleryDir) ||
                string.IsNullOrWhiteSpace(targetRoot))
            {
                return;
            }

            if (!File.Exists(sourceFile))
                return;

            var relative = GetRelativePathWithinGallery(galleryDir, sourceFile);
            var destination = Path.Combine(targetRoot, relative ?? Path.GetFileName(sourceFile));
            var destinationDir = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(destinationDir))
                Directory.CreateDirectory(destinationDir);

            token.ThrowIfCancellationRequested();
            File.Copy(sourceFile, destination, true);
        }

        private static string GetRelativePathWithinGallery(string galleryDir, string filePath)
        {
            if (!string.IsNullOrWhiteSpace(galleryDir) && !string.IsNullOrWhiteSpace(filePath))
            {
                try
                {
                    var relative = Path.GetRelativePath(galleryDir, filePath);
                    if (!string.IsNullOrEmpty(relative) &&
                        !string.Equals(relative, ".", StringComparison.Ordinal) &&
                        !relative.StartsWith("..", StringComparison.Ordinal))
                    {
                        return relative;
                    }
                }
                catch { }
            }

            return Path.GetFileName(filePath) ?? Guid.NewGuid().ToString("N");
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

        public IReadOnlyList<TemplateItem> GetTemplateLibraryTemplates()
        {
            if (DefaultTemplates.Count == 0)
            {
                return Array.Empty<TemplateItem>();
            }

            var allTemplates = DefaultTemplates.ToList();
            if (_allowedDefaultTemplateNames.Count == 0)
            {
                return allTemplates;
            }

            var allowedLookup = new HashSet<string>(_allowedDefaultTemplateNames, StringComparer.Ordinal);
            var filtered = allTemplates.Where(t => allowedLookup.Contains(t.Name)).ToList();
            return filtered.Count > 0 ? filtered : allTemplates;
        }

        public TemplateItem FindTemplateByName(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                return null;

            return Templates.FirstOrDefault(t => string.Equals(t.Name, templateName, StringComparison.OrdinalIgnoreCase));
        }

        public Task WaitForTemplateReloadAsync()
        {
            var task = _templateReloadTask;
            return task ?? Task.CompletedTask;
        }

        private void NormalizeAllowedTemplates()
        {
            NormalizeAllowedDevices(_allowedTemplateNames, Templates.Select(t => t.Name));
            OnPropertyChanged(nameof(AllowedTemplatesSummary));
        }

        private void NormalizeAllowedDefaultTemplates()
        {
            NormalizeAllowedDevices(_allowedDefaultTemplateNames, DefaultTemplates.Select(t => t.Name));

            if (!_defaultTemplatePermissionsConfigured && _allowedDefaultTemplateNames.Count == 0 && DefaultTemplates.Count > 0)
            {
                _allowedDefaultTemplateNames.AddRange(DefaultTemplates.Select(t => t.Name));
                _defaultTemplatePermissionsConfigured = true;
            }

            try { SettingsService.SaveAllowedDefaultTemplates(_allowedDefaultTemplateNames); } catch { }
        }

        private void NotifyDefaultTemplateChanged()
        {
            OnPropertyChanged(nameof(DefaultTemplate));
            OnPropertyChanged(nameof(DefaultTemplateName));
            OnPropertyChanged(nameof(HasDefaultTemplate));
            OnPropertyChanged(nameof(DefaultTemplateDisplayName));
            OnPropertyChanged(nameof(DefaultTemplatePreview));
            OnPropertyChanged(nameof(TemplateSlot1Template));
            OnPropertyChanged(nameof(TemplateSlot1Preview));
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

        private void EnsureDefaultTemplateAllowedByName(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                return;
            }

            if (_allowedDefaultTemplateNames.Any(name => string.Equals(name, templateName, StringComparison.Ordinal)))
            {
                return;
            }

            _allowedDefaultTemplateNames.Add(templateName);
            _defaultTemplatePermissionsConfigured = true;
            try { SettingsService.SaveAllowedDefaultTemplates(_allowedDefaultTemplateNames); } catch { }
        }

    }
}

