namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using ServiceControl.Hosting.Commands;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
// Mandatory, not stylistic: this assembly is Parallelizable(ParallelScope.All) and these fixtures set
// process-global environment variables. One fixture added without it makes the whole suite intermittent.
[NonParallelizable]
class When_a_copy_is_killed_mid_category : MigrationAcceptanceTest
{
    [Test]
    public void A_migrating_host_stopped_while_starting_exits_cleanly()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        Assert.DoesNotThrowAsync(async () =>
            await RunCommand.Run(Settings, AllowingAnUnreleasedMigration(StoppedWhileStarting), cancellation.Token));
    }

    [Test]
    public void A_host_not_migrating_stopped_while_starting_still_fails()
    {
        SetSourceVariable("SERVICECONTROL_MIGRATION_ENABLED", "false");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await RunCommand.Run(Settings, AllowingAnUnreleasedMigration(StoppedWhileStarting), cancellation.Token));
    }

    // Only a stop is a clean exit; a cancellation nobody asked for is still a failed start, migrating or not.
    [Test]
    public void A_migrating_host_whose_start_is_cancelled_without_a_stop_still_fails()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await RunCommand.Run(Settings, AllowingAnUnreleasedMigration(CancelledWithoutAStop), cancellation.Token));
    }

    [Test]
    public async Task It_resumes_rather_than_starting_over()
    {
        await SeedSourceKnownEndpoints("A", "B", "C", "D", "E");
        await SeedSourceEndpointSettings(("A", true), ("B", true), ("C", true), ("D", true), ("E", true));

        // Bounded rather than None: a copy that did not fail would otherwise hold this call open forever.
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        var killed = Assert.ThrowsAsync<Exception>(async () =>
            await RunCommand.Run(Settings, AllowingAnUnreleasedMigration(builder => builder.FailTheSecondMigrationWrite()), cancellation.Token));

        Assert.That(killed, Is.Not.Null);
        Assert.That(await TargetEndpointSettingsCount(), Is.EqualTo(2), "the first batch and its cursor should have committed together");

        await RunHostUntilTheApiAnswers();

        var checkpoint = await ReadCheckpoint("EndpointSettings");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await GetEndpointSettingsNames(), Is.EquivalentTo(new[] { "A", "B", "C", "D", "E" }), "no gaps");
            Assert.That(checkpoint.CopiedCount, Is.EqualTo(5), "no row copied twice");
            Assert.That(checkpoint.SkippedCount, Is.Zero);
            Assert.That(checkpoint.AlreadyPresentCount, Is.Zero, "a resumed run must not re-read rows the first run committed");
        }
    }

    // Proves the already-present count in the test above can fail: both runs end with five correct rows, so that count is the only thing telling a resume from a fresh copy.
    [Test]
    public async Task Without_its_checkpoint_the_second_run_re_reads_what_the_first_already_wrote()
    {
        await SeedSourceKnownEndpoints("A", "B", "C", "D", "E");
        await SeedSourceEndpointSettings(("A", true), ("B", true), ("C", true), ("D", true), ("E", true));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        Assert.ThrowsAsync<Exception>(async () =>
            await RunCommand.Run(Settings, AllowingAnUnreleasedMigration(builder => builder.FailTheSecondMigrationWrite()), cancellation.Token));

        await DeleteCheckpoint("EndpointSettings");
        await RunHostUntilTheApiAnswers();

        var checkpoint = await ReadCheckpoint("EndpointSettings");

        Assert.That(checkpoint.AlreadyPresentCount, Is.EqualTo(2));
    }

    // A copy stopped between two categories must still list the second, or every gate reading the table finds nothing outstanding.
    [Test]
    public async Task A_worker_started_after_a_copy_stopped_between_categories_names_the_one_it_never_reached()
    {
        await SeedSourceKnownEndpoints("A", "B");
        await SeedSourceEndpointSettings(("A", true), ("B", true));
        Settings.MaximumConcurrencyLevel = 1;

        using var stopping = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        Assert.CatchAsync<OperationCanceledException>(async () =>
            await RunCommand.Run(Settings, AllowingAnUnreleasedMigration(builder => builder.StopAfterTheFirstCategorySettles(stopping)), stopping.Token));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var worker = ErrorIngestionOnlyCommand.BuildHost(Settings, AllowingAnUnreleasedMigration());
        try
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That((await ReadCheckpoint(MigrationCategoryIds.KnownEndpoints))?.State, Is.EqualTo(MigrationCategoryState.Complete), "the stop has to land between the two categories for this test to mean anything");
                Assert.That(async () => await worker.StartAsync(cancellation.Token),
                    Throws.Exception.With.Message.Contain("EndpointSettings is NotStarted").And.Message.Not.Contain("KnownEndpoints is"),
                    "a worker let into a database whose copy stopped after its first category");
                Assert.That((await ReadCheckpoint(MigrationCategoryIds.EndpointSettings))?.State, Is.EqualTo(MigrationCategoryState.NotStarted));
            }
        }
        finally
        {
            await worker.DisposeAsync();
        }
    }

    Task<int> TargetEndpointSettingsCount() =>
        QueryTarget(dbContext => dbContext.EndpointSettings.CountAsync());

    Task<string[]> GetEndpointSettingsNames() =>
        QueryTarget(dbContext => dbContext.EndpointSettings.Select(settings => settings.Name).ToArrayAsync());

    static void StoppedWhileStarting(WebApplicationBuilder builder) =>
        builder.Services.AddHostedService(provider => new StopsTheHostWhileStarting(provider.GetRequiredService<IHostApplicationLifetime>()));

    static void CancelledWithoutAStop(WebApplicationBuilder builder) =>
        builder.Services.AddHostedService(_ => new CancelsWhileStarting());

    sealed class CancelsWhileStarting : IHostedLifecycleService
    {
        public Task StartingAsync(CancellationToken cancellationToken = default) => throw new OperationCanceledException();

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StartedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StoppingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StoppedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    // What a service stop or the critical-error handler does to a host that is still starting.
    sealed class StopsTheHostWhileStarting(IHostApplicationLifetime lifetime) : IHostedLifecycleService
    {
        public Task StartingAsync(CancellationToken cancellationToken = default)
        {
            lifetime.StopApplication();
            throw new OperationCanceledException(lifetime.ApplicationStopping);
        }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StartedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StoppingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StoppedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
