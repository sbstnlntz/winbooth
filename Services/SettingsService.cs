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
            public List<string> AllowedCameras { get; set; } = new();
            public List<string> AllowedPrinters { get; set; } = new();
            public List<string> AllowedTemplates { get; set; } = new();
            public bool CameraRotate180 { get; set; }
            public string UsbDrivePath { get; set; } = string.Empty;
        }

        private static string SettingsFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Fotobox");

        private static string SettingsPath => Path.Combine(SettingsFolder, "settings.json");

        private static SettingsModel LoadModel()
        {
            try
            {
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
            model.PostProcessDelaySeconds = Math.Max(0, seconds);
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
    }
}
