using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace winbooth.Utilities
{
    public sealed class PeriodicTaskHub : IDisposable
    {
        private readonly List<PeriodicTask> _tasks = new();
        private readonly Timer _timer;
        private readonly TimeSpan _tickInterval;
        private readonly object _lock = new();
        private readonly CancellationToken _token;
        private bool _disposed;

        public PeriodicTaskHub(TimeSpan tickInterval, CancellationToken token)
        {
            _tickInterval = tickInterval;
            _token = token;
            _timer = new Timer(OnTick, null, tickInterval, tickInterval);
        }

        public void RegisterTask(string name, TimeSpan interval, Func<CancellationToken, Task> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            lock (_lock)
            {
                _tasks.Add(new PeriodicTask
                {
                    Name = name ?? Guid.NewGuid().ToString("N"),
                    Interval = interval,
                    NextRunUtc = DateTime.UtcNow + interval,
                    Action = action
                });
            }
        }

        private void OnTick(object state)
        {
            if (_disposed || _token.IsCancellationRequested)
                return;

            List<PeriodicTask> dueTasks;
            var now = DateTime.UtcNow;
            lock (_lock)
            {
                dueTasks = _tasks
                    .Where(task => !task.IsRunning && now >= task.NextRunUtc)
                    .ToList();

                foreach (var task in dueTasks)
                {
                    task.IsRunning = true;
                    task.NextRunUtc = now + task.Interval;
                }
            }

            foreach (var task in dueTasks)
            {
                _ = ExecuteTaskAsync(task);
            }
        }

        private async Task ExecuteTaskAsync(PeriodicTask task)
        {
            try
            {
                await task.Action(_token).ConfigureAwait(false);
            }
            catch
            {
                // individual tasks handle their own exceptions
            }
            finally
            {
                task.IsRunning = false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            try { _timer.Dispose(); } catch { }
        }

        private sealed class PeriodicTask
        {
            public string Name { get; init; }
            public TimeSpan Interval { get; init; }
            public DateTime NextRunUtc { get; set; }
            public Func<CancellationToken, Task> Action { get; init; }
            public volatile bool IsRunning;
        }
    }
}
