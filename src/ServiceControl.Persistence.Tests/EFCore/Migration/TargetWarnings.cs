namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ServiceControl.Persistence.EFCore.DataMigration;

// RecordingLoggerProvider lives in ServiceControl.Infrastructure.Tests, which the persistence test projects do not reference.
/// <summary>
/// Keeps the lines the migration target logs, so a test can read what the operator sees. Messages holds the warnings and Information the information lines.
/// Register it as an ILoggerProvider through RegisterServices.
/// </summary>
sealed class TargetWarnings : ILoggerProvider
{
    readonly ConcurrentQueue<string> messages = new();
    readonly ConcurrentQueue<string> information = new();

    public IReadOnlyList<string> Messages => [.. messages];

    public IReadOnlyList<string> Information => [.. information];

    public ILogger CreateLogger(string categoryName) =>
        categoryName == typeof(EFCoreMigrationTarget).FullName ? new TargetLogger(this) : NullLogger.Instance;

    public void Dispose()
    {
    }

    sealed class TargetLogger(TargetWarnings lines) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel is LogLevel.Warning or LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                lines.messages.Enqueue(formatter(state, exception));
            }
            else if (logLevel == LogLevel.Information)
            {
                lines.information.Enqueue(formatter(state, exception));
            }
        }
    }
}
