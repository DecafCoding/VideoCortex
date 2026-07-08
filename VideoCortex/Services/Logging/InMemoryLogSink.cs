using System.Collections.Concurrent;

namespace VideoCortex.Services.Logging;

/// <summary>A single captured log event held by <see cref="InMemoryLogSink"/>.</summary>
public sealed record LogEntry(
    DateTime TimestampUtc,
    LogLevel Level,
    string Category,
    string Message,
    string? Exception);

/// <summary>
/// Bounded in-memory ring buffer of recent log events, readable by the Diagnostics page.
/// Singleton; oldest entries are dropped once <see cref="Capacity"/> is reached. Entries live
/// only for the process lifetime — the durable record is still the console/host log.
/// </summary>
public sealed class InMemoryLogSink
{
    public const int Capacity = 500;

    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public void Add(LogEntry entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > Capacity && _entries.TryDequeue(out _))
        {
        }
    }

    /// <summary>Snapshot of buffered entries at or above <paramref name="minLevel"/>, newest first.</summary>
    public IReadOnlyList<LogEntry> Snapshot(LogLevel minLevel = LogLevel.Information)
        => _entries.Where(e => e.Level >= minLevel).Reverse().ToList();
}

/// <summary>
/// <see cref="ILoggerProvider"/> that forwards every enabled log event into an
/// <see cref="InMemoryLogSink"/>. Level filtering is left to the standard Logging configuration.
/// </summary>
public sealed class InMemoryLoggerProvider(InMemoryLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new SinkLogger(sink, categoryName);

    public void Dispose()
    {
    }

    private sealed class SinkLogger(InMemoryLogSink sink, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            sink.Add(new LogEntry(
                DateTime.UtcNow, logLevel, category, formatter(state, exception), exception?.ToString()));
        }
    }
}
