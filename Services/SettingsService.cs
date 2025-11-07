using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FotoboxApp.Services
{
    public static class SettingsService
    {
        private class SettingsModel
        {
            public string GalleryName { get; set; } = string.Empty;
            public bool AllowTwoTemplates { get; set; }
            public bool AllowDirektdruck { get; set; }
            public bool AllowGalerie { get; set; } = true;
            public bool AllowFotoFilter { get; set; }
            public int PostProcessDelaySeconds { get; set; }
            public int StartReadyDelaySeconds { get; set; }
            public int CollageCreationDelaySeconds { get; set; }
            public List<string> AllowedCameras { get; set; } = new();
            public List<string> AllowedPrinters { get; set; } = new();
            public List<string> AllowedTemplates { get; set; } = new();
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
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures), "Fotobox");

        private static readonly string LegacySettingsPath = Path.Combine(LegacySettingsFolder, "settings.json");

        private static string SettingsFolder => AppStorage.EnsureDirectory("config");

        private static string SettingsPath => Path.Combine(SettingsFolder, "settings.json");

        private static SettingsModel LoadModel()
        {
            try
            {
                MigrateLegacySettingsIfNeeded();

                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var model = JsonSerializer.Deserialize<SettingsModel>(json);
                    return model ?? new SettingsModel();
                }
            }
            catch { }
            return new SettingsModel();
        }

        private static void SaveModel(SettingsModel model)
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);
                var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
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

        public static string LoadGalleryName() => LoadModel().GalleryName ?? string.Empty;

        public static bool LoadAllowTwoTemplates() => LoadModel().AllowTwoTemplates;
        public static bool LoadAllowDirektdruck() => LoadModel().AllowDirektdruck;
        public static bool LoadAllowGalerie() => LoadModel().AllowGalerie;
        public static bool LoadAllowFotoFilter() => LoadModel().AllowFotoFilter;
        public static int LoadPostProcessDelaySeconds() => LoadModel().PostProcessDelaySeconds;
        public static IReadOnlyList<string> LoadAllowedCameras()
        {
            var list = LoadModel().AllowedCameras ?? new List<string>();
            return list;
        }

        public static IReadOnlyList<string> LoadAllowedPrinters()
        {
            var list = LoadModel().AllowedPrinters ?? new List<string>();
            return list;
        }

        public static IReadOnlyList<string> LoadAllowedTemplates()
        {
            var list = LoadModel().AllowedTemplates ?? new List<string>();
            return list;
        }

        public static int LoadStartReadyDelaySeconds() => LoadModel().StartReadyDelaySeconds;
        public static int LoadCollageCreationDelaySeconds() => LoadModel().CollageCreationDelaySeconds;

        public static string LoadDefaultTemplateName() => LoadModel().DefaultTemplateName ?? string.Empty;

        public static string LoadUsbDrivePath() => LoadModel().UsbDrivePath ?? string.Empty;

        public static bool LoadCameraRotate180() => LoadModel().CameraRotate180;

        public static void SaveGalleryName(string name)
        {
            var model = LoadModel();
            model.GalleryName = name ?? string.Empty;
            SaveModel(model);
        }

        public static void SaveAllowTwoTemplates(bool allowTwoTemplates)
        {
            var model = LoadModel();
            model.AllowTwoTemplates = allowTwoTemplates;
            SaveModel(model);
        }

        public static void SaveAllowDirektdruck(bool allowDirektdruck)
        {
            var model = LoadModel();
            model.AllowDirektdruck = allowDirektdruck;
            SaveModel(model);
        }

        public static void SaveAllowGalerie(bool allowGalerie)
        {
            var model = LoadModel();
            model.AllowGalerie = allowGalerie;
            SaveModel(model);
        }

        public static void SaveAllowFotoFilter(bool allowFotoFilter)
        {
            var model = LoadModel();
            model.AllowFotoFilter = allowFotoFilter;
            SaveModel(model);
        }

        public static void SavePostProcessDelaySeconds(int seconds)
        {
            var model = LoadModel();
            model.PostProcessDelaySeconds = ClampDelay(seconds);
            SaveModel(model);
        }

        public static void SaveAllowedCameras(IEnumerable<string> cameras)
        {
            var model = LoadModel();
            model.AllowedCameras = cameras?.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList() ?? new List<string>();
            SaveModel(model);
        }

        public static void SaveAllowedPrinters(IEnumerable<string> printers)
        {
            var model = LoadModel();
            model.AllowedPrinters = printers?.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList() ?? new List<string>();
            SaveModel(model);
        }

        public static void SaveAllowedTemplates(IEnumerable<string> templates)
        {
            var model = LoadModel();
            model.AllowedTemplates = templates?.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList() ?? new List<string>();
            SaveModel(model);
        }

        public static void SaveStartReadyDelaySeconds(int seconds)
        {
            var model = LoadModel();
            model.StartReadyDelaySeconds = ClampDelay(seconds);
            SaveModel(model);
        }

        public static void SaveCollageCreationDelaySeconds(int seconds)
        {
            var model = LoadModel();
            model.CollageCreationDelaySeconds = ClampDelay(seconds);
            SaveModel(model);
        }

        public static void SaveDefaultTemplateName(string name)
        {
            var model = LoadModel();
            model.DefaultTemplateName = name ?? string.Empty;
            SaveModel(model);
        }

        public static void SaveUsbDrivePath(string path)
        {
            var model = LoadModel();
            model.UsbDrivePath = path ?? string.Empty;
            SaveModel(model);
        }

        public static void SaveCameraRotate180(bool rotate180)
        {
            var model = LoadModel();
            model.CameraRotate180 = rotate180;
            SaveModel(model);
        }

        public static bool LoadDirektdruckState() => LoadModel().DirektdruckState;

        public static bool LoadGalerieButtonState() => LoadModel().GalerieButtonState;

        public static bool LoadFotoFilterState() => LoadModel().FotoFilterState;

        public static void SaveDirektdruckState(bool value)
        {
            var model = LoadModel();
            model.DirektdruckState = value;
            SaveModel(model);
        }

        public static void SaveGalerieButtonState(bool value)
        {
            var model = LoadModel();
            model.GalerieButtonState = value;
            SaveModel(model);
        }

        public static void SaveFotoFilterState(bool value)
        {
            var model = LoadModel();
            model.FotoFilterState = value;
            SaveModel(model);
        }

        public static (string Template1, string Template2, string Active) LoadSelectedTemplateNames()
        {
            var model = LoadModel();
            return (
                model.SelectedTemplate1Name ?? string.Empty,
                model.SelectedTemplate2Name ?? string.Empty,
                model.ActiveTemplateName ?? string.Empty);
        }

        public static void SaveSelectedTemplateNames(string template1, string template2, string active)
        {
            var model = LoadModel();
            model.SelectedTemplate1Name = template1 ?? string.Empty;
            model.SelectedTemplate2Name = template2 ?? string.Empty;
            model.ActiveTemplateName = active ?? string.Empty;
            SaveModel(model);
        }

        public static void EnsureEventScopeForGallery(string galleryName)
        {
            var model = LoadModel();
            var current = model.EventScopeGalleryName ?? string.Empty;
            var target = galleryName ?? string.Empty;

            if (string.IsNullOrWhiteSpace(current))
            {
                model.EventScopeGalleryName = target;
                SaveModel(model);
                return;
            }

            if (string.Equals(current, target, StringComparison.Ordinal))
                return;

            ApplyEventDefaults(model);
            model.EventScopeGalleryName = target;
            SaveModel(model);
        }

        public static void ResetEventScopedValues(string galleryName)
        {
            var model = LoadModel();
            ApplyEventDefaults(model);
            model.EventScopeGalleryName = galleryName ?? string.Empty;
            SaveModel(model);
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
            model.SelectedTemplate1Name = string.Empty;
            model.SelectedTemplate2Name = string.Empty;
            model.ActiveTemplateName = string.Empty;
        }

        private static int ClampDelay(int seconds)
        {
            if (seconds < DelayMinSeconds)
                return DelayMinSeconds;
            if (seconds > DelayMaxSeconds)
                return DelayMaxSeconds;
            return seconds;
        }

        public static string LoadSelectedCameraName() => LoadModel().SelectedCameraName ?? string.Empty;
        public static string LoadSelectedPrinterName() => LoadModel().SelectedPrinterName ?? string.Empty;

        public static void SaveSelectedCameraName(string value)
        {
            var model = LoadModel();
            model.SelectedCameraName = value ?? string.Empty;
            SaveModel(model);
        }

        public static void SaveSelectedPrinterName(string value)
        {
            var model = LoadModel();
            model.SelectedPrinterName = value ?? string.Empty;
            SaveModel(model);
        }
    }
}
