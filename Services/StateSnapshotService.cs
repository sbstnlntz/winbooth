// Serializes and restores the StartViewModel UI state so sessions can resume.

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace winbooth.Services
{
    public sealed class StartViewStateSnapshot
    {
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        public string GalleryName { get; init; } = string.Empty;
        public string ActiveTemplateName { get; init; } = string.Empty;
        public string TemplateSlot1Name { get; init; } = string.Empty;
        public string TemplateSlot2Name { get; init; } = string.Empty;
        public string SelectedCameraName { get; init; } = string.Empty;
        public string SelectedPrinterName { get; init; } = string.Empty;
        public bool IsHardwareHealthy { get; init; }
        public DateTime LastCameraFrameUtc { get; init; }
        public DateTime LastUsbRefreshUtc { get; init; }
    }

    public static class StateSnapshotService
    {
        private static readonly string SnapshotFolder = AppStorage.EnsureDirectory("state");
        private static readonly string SnapshotPath = Path.Combine(SnapshotFolder, "startview_state.json");
        private static readonly SemaphoreSlim SnapshotSemaphore = new(1, 1);
        private static int _consecutiveWriteFailures;

        public static int ConsecutiveWriteFailures => Volatile.Read(ref _consecutiveWriteFailures);

        public static async Task SaveSnapshotAsync(StartViewStateSnapshot snapshot, CancellationToken token = default)
        {
            if (snapshot == null)
                return;

            await SnapshotSemaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(SnapshotFolder);
                var tempPath = Path.Combine(SnapshotFolder, $"startview_state_{Guid.NewGuid():N}.tmp");
                await using (var stream = new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough | FileOptions.Asynchronous))
                {
                    await JsonSerializer.SerializeAsync(stream, snapshot, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }, token).ConfigureAwait(false);
                    await stream.FlushAsync(token).ConfigureAwait(false);
                }

                File.Move(tempPath, SnapshotPath, overwrite: true);
                Interlocked.Exchange(ref _consecutiveWriteFailures, 0);
            }
            catch
            {
                Interlocked.Increment(ref _consecutiveWriteFailures);
                DiagnosticsLogger.LogError("Snapshot", "Speichern des StartView-Snapshots fehlgeschlagen");
                // Snapshotting is best-effort; ignore IO failures.
            }
            finally
            {
                SnapshotSemaphore.Release();
            }
        }
    }
}
