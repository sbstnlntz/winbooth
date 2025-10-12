using System;
using System.IO;
using System.Text.Json;

namespace FotoboxApp.Services
{
    public static class SettingsService
    {
        private class SettingsModel
        {
            public string GalleryName { get; set; }
        }

        private static string SettingsFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Fotobox");

        private static string SettingsPath => Path.Combine(SettingsFolder, "settings.json");

        public static string LoadGalleryName()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var model = JsonSerializer.Deserialize<SettingsModel>(json);
                    return model?.GalleryName ?? string.Empty;
                }
            }
            catch { }
            return string.Empty;
        }

        public static void SaveGalleryName(string name)
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);
                var model = new SettingsModel { GalleryName = name ?? string.Empty };
                var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}
