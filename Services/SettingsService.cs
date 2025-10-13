using System;
using System.IO;
using System.Text.Json;

namespace FotoboxApp.Services
{
    public static class SettingsService
    {
        private class SettingsModel
        {
            public string GalleryName { get; set; } = string.Empty;
            public bool AllowTwoTemplates { get; set; }
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
    }
}
