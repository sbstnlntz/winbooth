// Central settings persistence service supporting JSON storage and change notifications.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace winbooth.Services
{
    public static class SettingsService
    {
        public sealed class SettingsSnapshot
        {
            public string GalleryName { get; init; }
            public bool AllowTwoTemplates { get; init; }
            public bool AllowDirektdruck { get; init; }
            public bool AllowGalerie { get; init; }
            public bool AllowFotoFilter { get; init; }
            public int PostProcessDelaySeconds { get; init; }
            public int StartReadyDelaySeconds { get; init; }
            public int CollageCreationDelaySeconds { get; init; }
            public IReadOnlyList<string> AllowedCameras { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> AllowedPrinters { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> AllowedTemplates { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> AllowedDefaultTemplates { get; init; } = Array.Empty<string>();
            public string DefaultTemplateName { get; init; }
            public bool CameraRotate180 { get; init; }
            public string UsbDrivePath { get; init; }
            public string EventScopeGalleryName { get; init; }
            public bool DirektdruckState { get; init; }
            public bool GalerieButtonState { get; init; }
            public bool FotoFilterState { get; init; }
            public string SelectedTemplate1Name { get; init; }
            public string SelectedTemplate2Name { get; init; }
            public string ActiveTemplateName { get; init; }
            public string SelectedCameraName { get; init; }
            public string SelectedPrinterName { get; init; }
        }

        private class SettingsModel
        {
            public string GalleryName { get; set; } = string.Empty;
            public bool AllowTwoTemplates { get; set; }
            public bool AllowDirektdruck { get; set; }
            public bool AllowGalerie { get; set; } = true;
            public bool AllowFotoFilter { get; set; }
            public int PostProcessDelaySeconds { get; set; } = DefaultDelaySeconds;
            public int StartReadyDelaySeconds { get; set; } = DefaultDelaySeconds;
            public int CollageCreationDelaySeconds { get; set; } = DefaultDelaySeconds;
            public List<string> AllowedCameras { get; set; } = new();
            public List<string> AllowedPrinters { get; set; } = new();
            public List<string> AllowedTemplates { get; set; } = new();
            public List<string> AllowedDefaultTemplates { get; set; } = new();
            public string DefaultTemplateName { get; set; } = string.Empty;
            public bool CameraRotate180 { get; set; }
            public string UsbDrivePath { get; set; } = string.Empty;
            public string EventScopeGalleryName { get; set; } = string.Empty;
            public bool DirektdruckState { get; set; } = true;
            public bool GalerieButtonState { get; set; } = true;
            public bool FotoFilterState { get; set; } = true;
            public string SelectedTemplate1Name { get; set; } = string.Empty;
            public string SelectedTemplate2Name { get; set; } = string.Empty;
            public string ActiveTemplateName { get; set; } = string.Empty;
            public string SelectedCameraName { get; set; } = string.Empty;
            public string SelectedPrinterName { get; set; } = string.Empty;
        }

        private const int DelayMinSeconds = 1;
        private const int DelayMaxSeconds = 10;
        private const int DefaultDelaySeconds = 3;

        private static readonly string LegacySettingsFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Fotobox");
        private static readonly string LegacySettingsPath = Path.Combine(LegacySettingsFolder, "settings.json");

        private static string SettingsFolder => AppStorage.EnsureDirectory("config");
        private static string SettingsPath => Path.Combine(SettingsFolder, "settings.json");

        private static readonly object ModelLock = new();
        private static SettingsModel _model = LoadModelFromDisk();
        private static SettingsModel _pendingSnapshot;
        private static int _persistLoopActive;
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true
        };

        private static SettingsModel EnsureModel()
        {
            if (_model != null)
                return _model;

            lock (ModelLock)
            {
                _model ??= LoadModelFromDisk();
                return _model;
            }
        }

        private static SettingsModel LoadModelFromDisk()
        {
            try
            {
                MigrateLegacySettingsIfNeeded();
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var model = JsonSerializer.Deserialize<SettingsModel>(json);
                    return NormalizeModel(model ?? new SettingsModel());
                }
            }
            catch
            {
                // fall back to defaults
            }

            return NormalizeModel(new SettingsModel());
        }

        private static SettingsModel NormalizeModel(SettingsModel model)
        {
            model.PostProcessDelaySeconds = ClampDelay(model.PostProcessDelaySeconds);
            model.StartReadyDelaySeconds = ClampDelay(model.StartReadyDelaySeconds);
            model.CollageCreationDelaySeconds = ClampDelay(model.CollageCreationDelaySeconds);
            model.AllowedCameras ??= new List<string>();
            model.AllowedPrinters ??= new List<string>();
            model.AllowedTemplates ??= new List<string>();
            model.SelectedTemplate1Name ??= string.Empty;
            model.SelectedTemplate2Name ??= string.Empty;
            model.ActiveTemplateName ??= string.Empty;
            model.SelectedCameraName ??= string.Empty;
            model.SelectedPrinterName ??= string.Empty;
            model.DefaultTemplateName ??= string.Empty;
            model.UsbDrivePath ??= string.Empty;
            model.GalleryName ??= string.Empty;
            model.EventScopeGalleryName ??= string.Empty;
            return model;
        }

        private static SettingsModel CloneModel(SettingsModel source)
        {
            return new SettingsModel
            {
                GalleryName = source.GalleryName,
                AllowTwoTemplates = source.AllowTwoTemplates,
                AllowDirektdruck = source.AllowDirektdruck,
                AllowGalerie = source.AllowGalerie,
                AllowFotoFilter = source.AllowFotoFilter,
                PostProcessDelaySeconds = source.PostProcessDelaySeconds,
                StartReadyDelaySeconds = source.StartReadyDelaySeconds,
                CollageCreationDelaySeconds = source.CollageCreationDelaySeconds,
                AllowedCameras = new List<string>(source.AllowedCameras),
                AllowedPrinters = new List<string>(source.AllowedPrinters),
                AllowedTemplates = new List<string>(source.AllowedTemplates),
                DefaultTemplateName = source.DefaultTemplateName,
                CameraRotate180 = source.CameraRotate180,
                UsbDrivePath = source.UsbDrivePath,
                EventScopeGalleryName = source.EventScopeGalleryName,
                DirektdruckState = source.DirektdruckState,
                GalerieButtonState = source.GalerieButtonState,
                FotoFilterState = source.FotoFilterState,
                SelectedTemplate1Name = source.SelectedTemplate1Name,
                SelectedTemplate2Name = source.SelectedTemplate2Name,
                ActiveTemplateName = source.ActiveTemplateName,
                SelectedCameraName = source.SelectedCameraName,
                SelectedPrinterName = source.SelectedPrinterName
            };
        }

        private static void SchedulePersistLocked()
        {
            _pendingSnapshot = CloneModel(_model);
            if (Interlocked.CompareExchange(ref _persistLoopActive, 1, 0) == 0)
            {
                _ = Task.Run(PersistLoopAsync);
            }
        }

        private static async Task PersistLoopAsync()
        {
            while (true)
            {
                SettingsModel snapshot;
                lock (ModelLock)
                {
                    snapshot = _pendingSnapshot;
                    _pendingSnapshot = null;
                }

                if (snapshot == null)
                    break;

                await PersistModelAsync(snapshot).ConfigureAwait(false);
            }

            Interlocked.Exchange(ref _persistLoopActive, 0);

            if (Volatile.Read(ref _pendingSnapshot) != null &&
                Interlocked.CompareExchange(ref _persistLoopActive, 1, 0) == 0)
            {
                _ = Task.Run(PersistLoopAsync);
            }
        }

        private static async Task PersistModelAsync(SettingsModel snapshot)
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);
                var tempPath = Path.Combine(SettingsFolder, $"settings_{Guid.NewGuid():N}.tmp");

                await using (var stream = new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough | FileOptions.Asynchronous))
                {
                    await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);
                }

                File.Move(tempPath, SettingsPath, overwrite: true);
            }
            catch
            {
                // persistence best effort
            }
        }

        public static SettingsSnapshot CaptureSnapshot()
        {
            lock (ModelLock)
            {
                return CreateSnapshot(EnsureModel());
            }
        }

        private static SettingsSnapshot CreateSnapshot(SettingsModel model)
        {
            if (model == null)
                return new SettingsSnapshot();

            return new SettingsSnapshot
            {
                GalleryName = model.GalleryName,
                AllowTwoTemplates = model.AllowTwoTemplates,
                AllowDirektdruck = model.AllowDirektdruck,
                AllowGalerie = model.AllowGalerie,
                AllowFotoFilter = model.AllowFotoFilter,
                PostProcessDelaySeconds = model.PostProcessDelaySeconds,
                StartReadyDelaySeconds = model.StartReadyDelaySeconds,
                CollageCreationDelaySeconds = model.CollageCreationDelaySeconds,
                AllowedCameras = (model.AllowedCameras ?? new List<string>()).ToArray(),
                AllowedPrinters = (model.AllowedPrinters ?? new List<string>()).ToArray(),
                AllowedTemplates = (model.AllowedTemplates ?? new List<string>()).ToArray(),
                AllowedDefaultTemplates = (model.AllowedDefaultTemplates ?? new List<string>()).ToArray(),
                DefaultTemplateName = model.DefaultTemplateName,
                CameraRotate180 = model.CameraRotate180,
                UsbDrivePath = model.UsbDrivePath,
                EventScopeGalleryName = model.EventScopeGalleryName,
                DirektdruckState = model.DirektdruckState,
                GalerieButtonState = model.GalerieButtonState,
                FotoFilterState = model.FotoFilterState,
                SelectedTemplate1Name = model.SelectedTemplate1Name,
                SelectedTemplate2Name = model.SelectedTemplate2Name,
                ActiveTemplateName = model.ActiveTemplateName,
                SelectedCameraName = model.SelectedCameraName,
                SelectedPrinterName = model.SelectedPrinterName
            };
        }

        private static void MigrateLegacySettingsIfNeeded()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return;

                if (File.Exists(LegacySettingsPath))
                {
                    Directory.CreateDirectory(SettingsFolder);
                    File.Copy(LegacySettingsPath, SettingsPath, overwrite: false);
                    try { File.Delete(LegacySettingsPath); } catch { }
                }
            }
            catch
            {
                // migration best effort
            }
        }

        public static string LoadGalleryName()
        {
            lock (ModelLock)
            {
                return EnsureModel().GalleryName;
            }
        }

        public static bool LoadAllowTwoTemplates()
        {
            lock (ModelLock)
            {
                return EnsureModel().AllowTwoTemplates;
            }
        }

        public static bool LoadAllowDirektdruck()
        {
            lock (ModelLock)
            {
                return EnsureModel().AllowDirektdruck;
            }
        }

        public static bool LoadAllowGalerie()
        {
            lock (ModelLock)
            {
                return EnsureModel().AllowGalerie;
            }
        }

        public static bool LoadAllowFotoFilter()
        {
            lock (ModelLock)
            {
                return EnsureModel().AllowFotoFilter;
            }
        }

        public static int LoadPostProcessDelaySeconds()
        {
            lock (ModelLock)
            {
                return EnsureModel().PostProcessDelaySeconds;
            }
        }

        public static IReadOnlyList<string> LoadAllowedCameras()
        {
            lock (ModelLock)
            {
                return EnsureModel().AllowedCameras.ToArray();
            }
        }

        public static IReadOnlyList<string> LoadAllowedPrinters()
        {
            lock (ModelLock)
            {
                return EnsureModel().AllowedPrinters.ToArray();
            }
        }

        public static IReadOnlyList<string> LoadAllowedTemplates()
        {
            lock (ModelLock)
            {
                return EnsureModel().AllowedTemplates.ToArray();
            }
        }

        public static IReadOnlyList<string> LoadAllowedDefaultTemplates()
        {
            lock (ModelLock)
            {
                return EnsureModel().AllowedDefaultTemplates.ToArray();
            }
        }

        public static int LoadStartReadyDelaySeconds()
        {
            lock (ModelLock)
            {
                return EnsureModel().StartReadyDelaySeconds;
            }
        }

        public static int LoadCollageCreationDelaySeconds()
        {
            lock (ModelLock)
            {
                return EnsureModel().CollageCreationDelaySeconds;
            }
        }

        public static string LoadDefaultTemplateName()
        {
            lock (ModelLock)
            {
                return EnsureModel().DefaultTemplateName;
            }
        }

        public static string LoadUsbDrivePath()
        {
            lock (ModelLock)
            {
                return EnsureModel().UsbDrivePath;
            }
        }

        public static bool LoadCameraRotate180()
        {
            lock (ModelLock)
            {
                return EnsureModel().CameraRotate180;
            }
        }

        public static void SaveGalleryName(string name)
        {
            lock (ModelLock)
            {
                EnsureModel().GalleryName = name ?? string.Empty;
                SchedulePersistLocked();
            }
        }

        public static void SaveAllowTwoTemplates(bool allowTwoTemplates)
        {
            lock (ModelLock)
            {
                EnsureModel().AllowTwoTemplates = allowTwoTemplates;
                SchedulePersistLocked();
            }
        }

        public static void SaveAllowDirektdruck(bool allowDirektdruck)
        {
            lock (ModelLock)
            {
                EnsureModel().AllowDirektdruck = allowDirektdruck;
                SchedulePersistLocked();
            }
        }

        public static void SaveAllowGalerie(bool allowGalerie)
        {
            lock (ModelLock)
            {
                EnsureModel().AllowGalerie = allowGalerie;
                SchedulePersistLocked();
            }
        }

        public static void SaveAllowFotoFilter(bool allowFotoFilter)
        {
            lock (ModelLock)
            {
                EnsureModel().AllowFotoFilter = allowFotoFilter;
                SchedulePersistLocked();
            }
        }

        public static void SavePostProcessDelaySeconds(int seconds)
        {
            lock (ModelLock)
            {
                EnsureModel().PostProcessDelaySeconds = ClampDelay(seconds);
                SchedulePersistLocked();
            }
        }

        public static void SaveAllowedCameras(IEnumerable<string> cameras)
        {
            lock (ModelLock)
            {
                EnsureModel().AllowedCameras = cameras?
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.Ordinal)
                    .ToList() ?? new List<string>();
                SchedulePersistLocked();
            }
        }

        public static void SaveAllowedPrinters(IEnumerable<string> printers)
        {
            lock (ModelLock)
            {
                EnsureModel().AllowedPrinters = printers?
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.Ordinal)
                    .ToList() ?? new List<string>();
                SchedulePersistLocked();
            }
        }

        public static void SaveAllowedTemplates(IEnumerable<string> templates)
        {
            lock (ModelLock)
            {
                EnsureModel().AllowedTemplates = templates?
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct(StringComparer.Ordinal)
                    .ToList() ?? new List<string>();
                SchedulePersistLocked();
            }
        }

        public static void SaveAllowedDefaultTemplates(IEnumerable<string> templates)
        {
            lock (ModelLock)
            {
                EnsureModel().AllowedDefaultTemplates = templates?
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct(StringComparer.Ordinal)
                    .ToList() ?? new List<string>();
                SchedulePersistLocked();
            }
        }

        public static void SaveStartReadyDelaySeconds(int seconds)
        {
            lock (ModelLock)
            {
                EnsureModel().StartReadyDelaySeconds = ClampDelay(seconds);
                SchedulePersistLocked();
            }
        }

        public static void SaveCollageCreationDelaySeconds(int seconds)
        {
            lock (ModelLock)
            {
                EnsureModel().CollageCreationDelaySeconds = ClampDelay(seconds);
                SchedulePersistLocked();
            }
        }

        public static void SaveDefaultTemplateName(string name)
        {
            lock (ModelLock)
            {
                EnsureModel().DefaultTemplateName = name ?? string.Empty;
                SchedulePersistLocked();
            }
        }

        public static void SaveUsbDrivePath(string path)
        {
            lock (ModelLock)
            {
                EnsureModel().UsbDrivePath = path ?? string.Empty;
                SchedulePersistLocked();
            }
        }

        public static void SaveCameraRotate180(bool rotate180)
        {
            lock (ModelLock)
            {
                EnsureModel().CameraRotate180 = rotate180;
                SchedulePersistLocked();
            }
        }

        public static bool LoadDirektdruckState()
        {
            lock (ModelLock)
            {
                return EnsureModel().DirektdruckState;
            }
        }

        public static bool LoadGalerieButtonState()
        {
            lock (ModelLock)
            {
                return EnsureModel().GalerieButtonState;
            }
        }

        public static bool LoadFotoFilterState()
        {
            lock (ModelLock)
            {
                return EnsureModel().FotoFilterState;
            }
        }

        public static void SaveDirektdruckState(bool value)
        {
            lock (ModelLock)
            {
                EnsureModel().DirektdruckState = value;
                SchedulePersistLocked();
            }
        }

        public static void SaveGalerieButtonState(bool value)
        {
            lock (ModelLock)
            {
                EnsureModel().GalerieButtonState = value;
                SchedulePersistLocked();
            }
        }

        public static void SaveFotoFilterState(bool value)
        {
            lock (ModelLock)
            {
                EnsureModel().FotoFilterState = value;
                SchedulePersistLocked();
            }
        }

        public static (string Template1, string Template2, string Active) LoadSelectedTemplateNames()
        {
            lock (ModelLock)
            {
                var model = EnsureModel();
                return (model.SelectedTemplate1Name, model.SelectedTemplate2Name, model.ActiveTemplateName);
            }
        }

        public static void SaveSelectedTemplateNames(string template1, string template2, string active)
        {
            lock (ModelLock)
            {
                var model = EnsureModel();
                model.SelectedTemplate1Name = template1 ?? string.Empty;
                model.SelectedTemplate2Name = template2 ?? string.Empty;
                model.ActiveTemplateName = active ?? string.Empty;
                SchedulePersistLocked();
            }
        }

        public static void EnsureEventScopeForGallery(string galleryName)
        {
            lock (ModelLock)
            {
                var model = EnsureModel();
                var current = model.EventScopeGalleryName ?? string.Empty;
                var target = galleryName ?? string.Empty;

                if (string.IsNullOrWhiteSpace(current))
                {
                    model.EventScopeGalleryName = target;
                    SchedulePersistLocked();
                    return;
                }

                if (string.Equals(current, target, StringComparison.Ordinal))
                    return;

                ApplyEventDefaults(model);
                model.EventScopeGalleryName = target;
                SchedulePersistLocked();
            }
        }

        public static void ResetEventScopedValues(string galleryName)
        {
            lock (ModelLock)
            {
                var model = EnsureModel();
                ApplyEventDefaults(model);
                model.EventScopeGalleryName = galleryName ?? string.Empty;
                SchedulePersistLocked();
            }
        }

        private static void ApplyEventDefaults(SettingsModel model)
        {
            model.AllowDirektdruck = false;
            model.AllowGalerie = true;
            model.AllowFotoFilter = false;
            model.AllowTwoTemplates = false;
            model.StartReadyDelaySeconds = DefaultDelaySeconds;
            model.CollageCreationDelaySeconds = DefaultDelaySeconds;
            model.PostProcessDelaySeconds = DefaultDelaySeconds;
            model.DirektdruckState = true;
            model.GalerieButtonState = true;
            model.FotoFilterState = true;
        }

        private static int ClampDelay(int seconds)
        {
            if (seconds < DelayMinSeconds)
                return DelayMinSeconds;
            if (seconds > DelayMaxSeconds)
                return DelayMaxSeconds;
            return seconds;
        }

        public static string LoadSelectedCameraName()
        {
            lock (ModelLock)
            {
                return EnsureModel().SelectedCameraName;
            }
        }

        public static string LoadSelectedPrinterName()
        {
            lock (ModelLock)
            {
                return EnsureModel().SelectedPrinterName;
            }
        }

        public static void SaveSelectedCameraName(string value)
        {
            lock (ModelLock)
            {
                EnsureModel().SelectedCameraName = value ?? string.Empty;
                SchedulePersistLocked();
            }
        }

        public static void SaveSelectedPrinterName(string value)
        {
            lock (ModelLock)
            {
                EnsureModel().SelectedPrinterName = value ?? string.Empty;
                SchedulePersistLocked();
            }
        }
    }
}
