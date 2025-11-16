// Thread safe statistics persistence that tracks shots, collages, and per event counts.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using winbooth.Services;

namespace winbooth.Utilities
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
            public int TotalEvents { get; init; }
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

        private static readonly string StatsFolder = AppStorage.EnsureDirectory("metrics");
        private static readonly string StatsFilePath = Path.Combine(StatsFolder, "stats.json");
        private static readonly string LegacyUserFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Fotobox");
        private static readonly string LegacyStatsFilePath = Path.Combine(LegacyUserFolder, "stats.json");
        private static readonly string LegacyCounterPath = Path.Combine(LegacyUserFolder, "counter.txt");

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true
        };

        private const string DefaultEventKey = "_default";
        private static readonly object ModelLock = new();
        private static StatsModel _modelCache = LoadModelFromDisk();
        private static StatsModel _pendingSnapshot;
        private static int _persistLoopActive;
        private static int _consecutivePersistFailures;

        public static int ConsecutivePersistFailures => Volatile.Read(ref _consecutivePersistFailures);

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
            lock (ModelLock)
            {
                var model = EnsureModel();
                var stats = TryGetEventStats(model, galleryName);

                return new StatsSnapshot
                {
                    TotalSinglePhotos = model.TotalSinglePhotos,
                    EventSinglePhotos = stats?.SinglePhotos ?? 0,
                    TotalCollagesCreated = model.TotalCollagesCreated,
                    EventCollagesCreated = stats?.CollagesCreated ?? 0,
                    TotalCollagesPrinted = model.TotalCollagesPrinted,
                    EventCollagesPrinted = stats?.CollagesPrinted ?? 0,
                    TotalEvents = CountTrackedEvents(model)
                };
            }
        }

        public static void ResetStatistics()
        {
            lock (ModelLock)
            {
                _modelCache = new StatsModel();
                SchedulePersistLocked();
            }

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
            if (apply == null)
                return;

            lock (ModelLock)
            {
                var model = EnsureModel();
                apply(model);
                SchedulePersistLocked();
            }
        }

        private static StatsModel EnsureModel()
        {
            if (_modelCache != null)
                return _modelCache;

            _modelCache = LoadModelFromDisk();
            return _modelCache;
        }

        private static void SchedulePersistLocked()
        {
            _pendingSnapshot = CloneModel(_modelCache);
            if (Interlocked.CompareExchange(ref _persistLoopActive, 1, 0) == 0)
            {
                _ = Task.Run(PersistLoopAsync);
            }
        }

        private static async Task PersistLoopAsync()
        {
            while (true)
            {
                StatsModel snapshot;
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

        private static async Task PersistModelAsync(StatsModel snapshot)
        {
            try
            {
                Directory.CreateDirectory(StatsFolder);
                var tempPath = Path.Combine(StatsFolder, $"stats_{Guid.NewGuid():N}.tmp");

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

                File.Move(tempPath, StatsFilePath, overwrite: true);
                Interlocked.Exchange(ref _consecutivePersistFailures, 0);
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _consecutivePersistFailures);
                DiagnosticsLogger.LogError("Stats", "Speichern der Statistiken fehlgeschlagen", ex);
                // stats persistence is best-effort
            }
        }

        private static StatsModel LoadModelFromDisk()
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

        private static StatsModel CloneModel(StatsModel source)
        {
            var clone = new StatsModel
            {
                SchemaVersion = source.SchemaVersion,
                TotalSinglePhotos = source.TotalSinglePhotos,
                TotalCollagesCreated = source.TotalCollagesCreated,
                TotalCollagesPrinted = source.TotalCollagesPrinted,
                Events = new Dictionary<string, EventStats>(source.Events.Count, StringComparer.OrdinalIgnoreCase)
            };

            foreach (var kvp in source.Events)
            {
                clone.Events[kvp.Key] = new EventStats
                {
                    SinglePhotos = kvp.Value?.SinglePhotos ?? 0,
                    CollagesCreated = kvp.Value?.CollagesCreated ?? 0,
                    CollagesPrinted = kvp.Value?.CollagesPrinted ?? 0
                };
            }

            return clone;
        }

        private static string NormalizeGalleryName(string galleryName)
        {
            var trimmed = (galleryName ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? DefaultEventKey : trimmed;
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

        private static int CountTrackedEvents(StatsModel model)
        {
            if (model?.Events == null || model.Events.Count == 0)
                return 0;

            var count = 0;
            foreach (var key in model.Events.Keys)
            {
                if (!string.Equals(key, DefaultEventKey, StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
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
