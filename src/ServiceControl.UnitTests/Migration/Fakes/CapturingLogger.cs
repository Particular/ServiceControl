#nullable enable
namespace ServiceControl.UnitTests.Migration.Fakes;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ServiceControl.Persistence.DataMigration;

public sealed class CapturingLogger : ILogger<MigrationEngine>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception), exception));
}
