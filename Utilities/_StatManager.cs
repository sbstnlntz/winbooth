using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FotoboxApp.Services;

namespace FotoboxApp.Utilities
{
    public static class StatManager
    {
        public sealed class StatsSnapshot
        {
            public int TotalSinglePhotos { get; init; }
            public int EventSinglePhotos { get; init; }
            public int TotalCollagesCreated { get; init; }
            public int EventCollagesCreated { get; init; }
            public int TotalCollagesPrinted { get; init; }
            public int EventCollagesPrinted { get; init; }
        }

        private sealed class StatsModel
        {
            public int SchemaVersion { get; set; } = 1;
            public Dictionary<string, EventStats> Events { get; set; } = CreateEventDictionary();
            public int TotalSinglePhotos { get; set; }
            public int TotalCollagesCreated { get; set; }
            public int TotalCollagesPrinted { get; set; }
        }

        private sealed class EventStats
        {
            public int SinglePhotos { get; set; }
            public int CollagesCreated { get; set; }
            public int CollagesPrinted { get; set; }
        }

        private static readonly object SyncRoot = new();

        private static readonly string StatsFolder = AppStorage.EnsureDirectory("metrics");
        private static readonly string StatsFilePath = Path.Combine(StatsFolder, "stats.json");
        private static readonly string LegacyUserFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Fotobox");
        private static readonly string LegacyStatsFilePath = Path.Combine(LegacyUserFolder, "stats.json");
        private static readonly string LegacyCounterPath = Path.Combine(LegacyUserFolder, "counter.txt");

        public static void RecordSinglePhoto(string galleryName)
            => UpdateModel(model =>
            {
                var stats = GetOrCreateEventStats(model, galleryName);
                stats.SinglePhotos++;
                model.TotalSinglePhotos++;
            });

        public static void RecordCollageCreated(string galleryName)
            => UpdateModel(model =>
            {
                var stats = GetOrCreateEventStats(model, galleryName);
                stats.CollagesCreated++;
                model.TotalCollagesCreated++;
            });

        public static void RecordCollagePrinted(string galleryName)
            => UpdateModel(model =>
            {
                var stats = GetOrCreateEventStats(model, galleryName);
                stats.CollagesPrinted++;
                model.TotalCollagesPrinted++;
            });

        public static StatsSnapshot GetStatsSnapshot(string galleryName)
        {
            lock (SyncRoot)
            {
                var model = LoadModel();
                var stats = TryGetEventStats(model, galleryName);

                return new StatsSnapshot
                {
                    TotalSinglePhotos = model.TotalSinglePhotos,
                    EventSinglePhotos = stats?.SinglePhotos ?? 0,
                    TotalCollagesCreated = model.TotalCollagesCreated,
                    EventCollagesCreated = stats?.CollagesCreated ?? 0,
                    TotalCollagesPrinted = model.TotalCollagesPrinted,
                    EventCollagesPrinted = stats?.CollagesPrinted ?? 0
                };
            }
        }

        public static void ResetStatistics()
        {
            lock (SyncRoot)
            {
                try
                {
                    if (File.Exists(StatsFilePath))
                        File.Delete(StatsFilePath);
                }
                catch { }

                try
                {
                    if (File.Exists(LegacyCounterPath))
                        File.Delete(LegacyCounterPath);
                    if (File.Exists(LegacyStatsFilePath))
                        File.Delete(LegacyStatsFilePath);
                }
                catch { }
            }
        }

        #region Legacy compatibility
        public static int GetTotalPhotoCount()
            => GetStatsSnapshot(null).TotalCollagesCreated;

        public static void IncreaseTotalPhotoCount()
            => RecordCollageCreated(null);

        public static void ResetTotalPhotoCount()
            => ResetStatistics();
        #endregion

        private static void UpdateModel(Action<StatsModel> apply)
        {
            lock (SyncRoot)
            {
                var model = LoadModel();
                apply?.Invoke(model);
                SaveModel(model);
            }
        }

        private static StatsModel LoadModel()
        {
            try
            {
                var current = TryLoadModelFromFile(StatsFilePath);
                if (current != null)
                    return current;

                if (File.Exists(LegacyStatsFilePath))
                {
                    Directory.CreateDirectory(StatsFolder);
                    try
                    {
                        File.Copy(LegacyStatsFilePath, StatsFilePath, overwrite: false);
                        File.Delete(LegacyStatsFilePath);
                    }
                    catch { }
                    current = TryLoadModelFromFile(StatsFilePath);
                    if (current != null)
                        return current;
                }
            }
            catch
            {
                // ignore and fall back
            }

            var fallback = new StatsModel();
            // Legacy migration: counter.txt stored number of collages created
            try
            {
                if (File.Exists(LegacyCounterPath))
                {
                    var content = File.ReadAllText(LegacyCounterPath);
                    if (int.TryParse(content, out var legacyCount))
                    {
                        fallback.TotalCollagesCreated = legacyCount;
                    }
                    try { File.Delete(LegacyCounterPath); } catch { }
                }
            }
            catch { }

            return fallback;
        }

        private static StatsModel TryLoadModelFromFile(string path)
        {
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<StatsModel>(json);
            if (model == null)
                return null;

            model.Events = CreateEventDictionary(model.Events);
            return model;
        }

        private static void SaveModel(StatsModel model)
        {
            try
            {
                Directory.CreateDirectory(StatsFolder);
                var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StatsFilePath, json);
            }
            catch
            {
                // ignore persistence errors
            }
        }

        private static string NormalizeGalleryName(string galleryName)
        {
            var trimmed = (galleryName ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? "_default" : trimmed;
        }

        private static EventStats GetOrCreateEventStats(StatsModel model, string galleryName)
        {
            var key = NormalizeGalleryName(galleryName);
            if (!model.Events.TryGetValue(key, out var stats) || stats == null)
            {
                stats = new EventStats();
                model.Events[key] = stats;
            }
            return stats;
        }

        private static EventStats TryGetEventStats(StatsModel model, string galleryName)
        {
            var key = NormalizeGalleryName(galleryName);
            return model.Events.TryGetValue(key, out var stats) ? stats : null;
        }

        private static Dictionary<string, EventStats> CreateEventDictionary(Dictionary<string, EventStats> source = null)
        {
            var dict = new Dictionary<string, EventStats>(StringComparer.OrdinalIgnoreCase);
            if (source == null)
                return dict;

            foreach (var kvp in source)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                    continue;
                dict[kvp.Key.Trim()] = kvp.Value ?? new EventStats();
            }
            return dict;
        }
    }
}
