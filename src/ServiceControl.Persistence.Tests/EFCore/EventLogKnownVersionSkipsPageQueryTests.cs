namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ServiceControl.EventLog;
using ServiceControl.Persistence.Infrastructure;

// EF only. RavenDB gets documents, total and etag from a single query, so it has nothing to skip
class EventLogKnownVersionSkipsPageQueryTests : PersistenceTestBase
{
    readonly CommandCounter counter = new();

    public EventLogKnownVersionSkipsPageQueryTests() =>
        RegisterServices = services => services.AddSingleton<ILoggerProvider>(counter);

    [Test]
    public async Task A_matching_known_version_does_not_run_the_page_query()
    {
        await EventLogDataStore.Add(CreateLogItem());
        await CompleteDatabaseOperation();

        var (_, _, version) = await EventLogDataStore.GetEventLogItems(new PagingInfo());

        counter.Reset();
        await EventLogDataStore.GetEventLogItems(new PagingInfo(), version);
        var whenCurrent = counter.Count;

        counter.Reset();
        await EventLogDataStore.GetEventLogItems(new PagingInfo());
        var whenFetching = counter.Count;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(whenCurrent, Is.EqualTo(2), "count and max only — the page query must not run");
            Assert.That(whenFetching, Is.EqualTo(3), "count, max and the page query");
        }
    }

    static EventLogItem CreateLogItem() => new()
    {
        Id = $"EventLogItem/Recoverability/MessageFailed/{Guid.NewGuid()}",
        Category = "Recoverability",
        EventType = "MessageFailed",
        Description = "failed",
        Severity = Severity.Info,
        RaisedAt = new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc),
        RelatedTo = []
    };

    sealed class CommandCounter : ILoggerProvider
    {
        int count;

        public int Count => Volatile.Read(ref count);

        public void Reset() => Volatile.Write(ref count, 0);

        // Only the relational command category matters; everything else the host logs is ignored.
        public ILogger CreateLogger(string categoryName) =>
            categoryName == DbLoggerCategory.Database.Command.Name
                ? new CountingLogger(this)
                : NullLogger.Instance;

        public void Dispose()
        {
        }

        sealed class CountingLogger(CommandCounter owner) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
                NullLogger.Instance.BeginScope(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                // CommandExecuted fires once per completed command, so it counts round trips rather
                // than retries or connection events.
                if (eventId == RelationalEventId.CommandExecuted)
                {
                    Interlocked.Increment(ref owner.count);
                }
            }
        }
    }
}
