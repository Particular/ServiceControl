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
using ServiceControl.Persistence.EFCore;

[TestFixture]
[NonParallelizable]
class When_a_copy_is_killed_mid_category : MigrationAcceptanceTest
{
    [Test]
    public void A_host_not_migrating_stopped_while_starting_still_fails()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await RunCommand.Run(Settings, StoppedWhileStarting, cancellation.Token));
    }

    [Test]
    public async Task It_resumes_rather_than_starting_over()
    {
        await SeedSourceKnownEndpoints("A", "B", "C", "D", "E");
        await SeedSourceEndpointSettings(("A", true), ("B", true), ("C", true), ("D", true), ("E", true));

        // Bounded rather than None: a copy that did not fail would otherwise hold this call open forever.
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        var killed = Assert.ThrowsAsync<Exception>(async () =>
            await MigrateCommand.Run(Settings, builder => builder.FailTheSecondMigrationWrite(), cancellation.Token));

        Assert.That(killed, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(killed.Message, Does.Contain("EndpointSettings").And.Contain("Halted"));
            Assert.That(killed.Message, Does.Contain("--retry=EndpointSettings").And.Contain("--abandon=EndpointSettings"), "a Failed category waits for the operator, so the refusal has to name what moves it on");
            Assert.That(await TargetEndpointSettingsCount(), Is.EqualTo(2), "the first batch and its cursor should have committed together");
        }

        var refusedAgain = Assert.ThrowsAsync<Exception>(async () =>
            await MigrateCommand.Run(Settings, null, cancellation.Token));
        Assert.That(refusedAgain.Message, Is.EqualTo(killed.Message), "a start re-ran a Failed category the operator had not put back");

        // The row a kill part way through the category leaves: still copying, with the first batch's cursor and counts.
        var halted = await ReadCheckpoint("EndpointSettings");
        await QueryTarget(dbContext => dbContext.UpsertCheckpoint(halted with { State = MigrationCategoryState.InProgress, LastError = null, SettledAt = null }));

        await MigrateThenRunHost();

        var checkpoint = await ReadCheckpoint("EndpointSettings");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await GetEndpointSettingsNames(), Is.EquivalentTo(new[] { "A", "B", "C", "D", "E" }), "no gaps");
            Assert.That(checkpoint.CopiedCount, Is.EqualTo(5), "no row copied twice");
            Assert.That(checkpoint.SkippedCount, Is.Zero);
            Assert.That(checkpoint.AlreadyPresentCount, Is.Zero, "a resumed run must not re-read rows the first run committed");
        }
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
            await MigrateCommand.Run(Settings, builder => builder.StopAfterTheFirstCategorySettles(stopping), stopping.Token));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var worker = ErrorIngestionOnlyCommand.BuildHost(Settings, null);
        try
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That((await ReadCheckpoint(MigrationCategoryIds.KnownEndpoints))?.State, Is.EqualTo(MigrationCategoryState.Complete), "the stop has to land between the two categories for this test to mean anything");
                Assert.That(async () => await worker.StartAsync(cancellation.Token),
                    Throws.Exception.With.Message.Contain("EndpointSettings is Copying (NotStarted)").And.Message.Not.Contain("KnownEndpoints is"),
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
