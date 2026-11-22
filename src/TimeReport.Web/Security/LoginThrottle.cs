using System.Collections.Concurrent;

namespace TimeReport.Web.Security;

/// <summary>
/// In-memory sliding-window throttle for password, MFA and setup attempts.
/// It is deliberately process-local: a restart clears the counters, while
/// persistent lockouts are handled by account state.
/// </summary>
public sealed class LoginThrottle
{
    private readonly ConcurrentDictionary<string, AttemptWindow> _windows = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public LoginThrottle(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsBlocked(string key, int limit, TimeSpan window)
    {
        CleanupIfNeeded(window);
        if (!_windows.TryGetValue(key, out var attemptWindow))
        {
            return false;
        }

        lock (attemptWindow)
        {
            attemptWindow.RemoveExpired(Now(), window);
            return attemptWindow.Count >= limit;
        }
    }

    public void RecordFailure(string key, TimeSpan window)
    {
        CleanupIfNeeded(window);
        var attemptWindow = _windows.GetOrAdd(key, _ => new AttemptWindow());
        lock (attemptWindow)
        {
            attemptWindow.Add(Now(), window);
        }
    }

    public void Reset(string key) => _windows.TryRemove(key, out _);

    private DateTimeOffset Now() => _timeProvider.GetUtcNow();

    private void CleanupIfNeeded(TimeSpan window)
    {
        if (_windows.Count < 5_000)
        {
            return;
        }

        var now = Now();
        foreach (var (key, value) in _windows)
        {
            lock (value)
            {
                value.RemoveExpired(now, window);
                if (value.Count == 0)
                {
                    _windows.TryRemove(key, out _);
                }
            }
        }
    }

    private sealed class AttemptWindow
    {
        private readonly Queue<DateTimeOffset> _attempts = new();

        public int Count
        {
            get
            {
                lock (_attempts)
                {
                    return _attempts.Count;
                }
            }
        }

        public void Add(DateTimeOffset at, TimeSpan window)
        {
            lock (_attempts)
            {
                RemoveExpiredCore(at, window);
                _attempts.Enqueue(at);
            }
        }

        public void RemoveExpired(DateTimeOffset now, TimeSpan window)
        {
            lock (_attempts)
            {
                RemoveExpiredCore(now, window);
            }
        }

        private void RemoveExpiredCore(DateTimeOffset now, TimeSpan window)
        {
            var cutoff = now - window;
            while (_attempts.Count > 0 && _attempts.Peek() < cutoff)
            {
                _attempts.Dequeue();
            }
        }
    }
}
