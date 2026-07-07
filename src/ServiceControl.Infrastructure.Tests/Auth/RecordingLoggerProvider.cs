#nullable enable
namespace ServiceControl.Infrastructure.Tests.Auth;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

/// <summary>
/// In-memory <see cref="ILoggerProvider"/> that captures log entries for test assertions.
/// Thread-safe. Use <see cref="Entries"/> for all captured entries; <see cref="EntriesFor(string)"/>
/// to filter by category.
/// </summary>
sealed class RecordingLoggerProvider : ILoggerProvider
{
    readonly ConcurrentQueue<LogEntry> entries = new();

    public IReadOnlyList<LogEntry> Entries => entries.ToArray();

    public IReadOnlyList<LogEntry> EntriesFor(string category) =>
        entries.Where(e => e.Category == category).ToArray();

    public ILogger CreateLogger(string categoryName) =>
        new RecordingLogger(categoryName, entries);

    public void Dispose() { }
}

sealed record LogEntry(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception);

sealed class RecordingLogger(string category, ConcurrentQueue<LogEntry> sink) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        sink.Enqueue(new LogEntry(category, logLevel, eventId, message, exception));
    }

    sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
