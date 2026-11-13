namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using ServiceControl.Migration;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class OptionalCategoryCopierTests
{
    [Test]
    public async Task A_persister_without_checkpoints_needs_no_source_or_target()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await Run(services);
    }

    [Test]
    public async Task A_database_that_never_migrated_needs_no_source_or_target()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IMigrationCheckpointStore>(new InMemoryMigrationCheckpointStore())
            .BuildServiceProvider();
        await Run(services);
    }

    [TestCase(MigrationCategoryState.Complete)]
    [TestCase(MigrationCategoryState.Abandoned)]
    [TestCase(MigrationCategoryState.Halted)]
    [TestCase(MigrationCategoryState.CompleteWithErrors)]
    public async Task A_finished_or_failed_optional_category_does_not_open_the_source(MigrationCategoryState state)
    {
        var store = new InMemoryMigrationCheckpointStore();
        var checkpoint = await store.Upsert(new MigrationCheckpoint(MigrationCategoryIds.EventLog, state, null, 0, 0, null, null, null, null, null, null));
        using var services = new ServiceCollection().AddSingleton<IMigrationCheckpointStore>(store).BuildServiceProvider();
        await Run(services);
        Assert.That(await store.Read(checkpoint.CategoryId), Is.EqualTo(checkpoint));
    }

    static async Task Run(ServiceProvider services)
    {
        var logger = new ErrorLogger();
        using var copier = new OptionalCategoryCopier(services, null, new StartedLifetime(), logger);
        await copier.StartAsync(CancellationToken.None);
        await copier.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(10));
        await copier.StopAsync(CancellationToken.None);
        Assert.That(logger.Errors, Is.Empty, "an idle copier must not attempt to resolve or open a source or target");
    }

    sealed class ErrorLogger : ILogger<OptionalCategoryCopier>
    {
        public List<string> Errors { get; } = [];
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }
    }

    sealed class StartedLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => new(true);
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }
}
