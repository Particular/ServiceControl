namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ServiceControl.Hosting.Commands;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.Tests;

[TestFixture]
[NonParallelizable]
class When_the_copy_merges_or_changes_rows : MigrationAcceptanceTest
{
    [Test]
    public async Task The_category_summary_counts_changed_and_merged_rows_apart_from_the_rest()
    {
        var retried = new IngestedFailure { MessageType = new string('m', 600) };
        await SeedSourceWithBody(retried.ToFailedMessage(numberOfAttempts: 2), retried.Body, retried.ContentType);
        await SeedSourceSubscriptions(
            new SubscriptionRow("MyMessages.OrderPlaced", new Version(1, 0, 0), "Billing@BILLING01", "Billing"),
            new SubscriptionRow("MyMessages.OrderPlaced", new Version(2, 0, 0), "Billing@BILLING01", "Billing"));
        var summaries = new SummaryLines();

        await ExpectExitCode(MigrateCommand.Run(MigrationSettings(), builder => builder.Services.AddSingleton<ILoggerProvider>(summaries)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summaries.Lines, Has.Exactly(1).EqualTo((LogLevel.Warning, $"{MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages}: 1 copied (1 changed), 0 already present, 0 merged, nothing skipped")),
                "the message type was cut, which the summary has to count even though nothing was skipped, and the dropped older attempt is not a change");
            Assert.That(summaries.Lines, Has.Exactly(1).EqualTo((LogLevel.Warning, $"{MigrationCategoryIds.Subscriptions}: 1 copied (0 changed), 0 already present, 1 merged, nothing skipped")),
                "the second version is counted as already present by the checkpoint, and the summary has to show it as a merge instead");
        }
    }

    // Keeps the lines MigrationStartup logs, which is where the summary is written.
    sealed class SummaryLines : ILoggerProvider
    {
        readonly ConcurrentQueue<(LogLevel Level, string Message)> lines = new();

        public (LogLevel Level, string Message)[] Lines => [.. lines];

        public ILogger CreateLogger(string categoryName) =>
            categoryName == typeof(MigrationStartup).FullName ? new SummaryLogger(lines) : NullLogger.Instance;

        public void Dispose()
        {
        }

        sealed class SummaryLogger(ConcurrentQueue<(LogLevel Level, string Message)> lines) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) =>
                lines.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}
