// Lightweight logging helper that writes diagnostic messages and errors to disk.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace winbooth.Services
{
    public static class DiagnosticsLogger
    {
        private static readonly string LogDirectory = AppStorage.EnsureDirectory("logs");
        private static readonly string LogFilePath = Path.Combine(LogDirectory, "winbooth.log");
        private static readonly BlockingCollection<string> PendingEntries = new(new ConcurrentQueue<string>());
        private static readonly CancellationTokenSource WriterCts = new();
        private static readonly Task WriterTask = Task.Run(ProcessQueueAsync);

        public static void LogInfo(string category, string message) =>
            Enqueue("INFO", category, message, null);

        public static void LogWarning(string category, string message, Exception exception = null) =>
            Enqueue("WARN", category, message, exception);

        public static void LogError(string category, string message, Exception exception = null) =>
            Enqueue("ERROR", category, message, exception);

        public static void Shutdown()
        {
            try
            {
                PendingEntries.CompleteAdding();
                WriterCts.Cancel();
                WriterTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // ignore shutdown errors
            }
        }

        private static void Enqueue(string level, string category, string message, Exception exception)
        {
            var line = BuildLine(level, category, message, exception);
            try
            {
                PendingEntries.Add(line);
            }
            catch
            {
                // swallow if queue already completed
            }
        }

        private static string BuildLine(string level, string category, string message, Exception exception)
        {
            var builder = new StringBuilder();
            builder.Append('[')
                   .Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                   .Append("][")
                   .Append(level)
                   .Append("][")
                   .Append(category ?? "General")
                   .Append("] ")
                   .Append(message ?? string.Empty);

            if (exception != null)
            {
                builder.Append(" :: ")
                    .Append(exception.GetType().Name)
                    .Append(": ")
                    .Append(exception.Message);
            }

            return builder.ToString();
        }

        private static async Task ProcessQueueAsync()
        {
            StreamWriter writer = null;
            try
            {
                Directory.CreateDirectory(LogDirectory);
                var stream = new FileStream(
                    LogFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    8192,
                    FileOptions.Asynchronous | FileOptions.WriteThrough);
                writer = new StreamWriter(stream) { AutoFlush = true };

                foreach (var entry in PendingEntries.GetConsumingEnumerable(WriterCts.Token))
                {
                    try
                    {
                        await writer.WriteLineAsync(entry).ConfigureAwait(false);
                    }
                    catch
                    {
                        // logging must never throw
                    }
                }
            }
            catch
            {
                // logger startup best-effort
            }
            finally
            {
                writer?.Dispose();
            }
        }
    }
}
