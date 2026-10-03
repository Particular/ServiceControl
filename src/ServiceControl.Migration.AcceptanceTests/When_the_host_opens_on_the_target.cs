namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Hosting.Commands;

[TestFixture]
// Mandatory, not stylistic: this assembly is Parallelizable(ParallelScope.All) and these fixtures set
// process-global environment variables. One fixture added without it makes the whole suite intermittent.
[NonParallelizable]
class When_the_host_opens_on_the_target : MigrationAcceptanceTest
{
    const string HostOpenedSetting = "Migration/HostOpenedOnTarget";

    [Test]
    public async Task The_row_does_not_exist_while_the_required_copy_is_still_running()
    {
        await SeedSourceKnownEndpoints("Sales");
        await SeedSourceEndpointSettings(("Sales", true));

        var copyIsParked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTheCopy = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var host = RunCommand.Run(Settings, AllowingAnUnreleasedMigration(builder => builder.ParkFirstMigrationWrite(copyIsParked, releaseTheCopy.Task)), cancellation.Token);

        await copyIsParked.Task.WaitAsync(TimeSpan.FromMinutes(2));
        Assert.That(await ReadSetting(HostOpenedSetting), Is.Null, "the abort is still free at this moment, and this row is what says it is not");

        releaseTheCopy.SetResult();
        await WaitForEndpointSettingsResponse(TimeSpan.FromMinutes(2));

        Assert.That(await ReadSetting(HostOpenedSetting), Is.Not.Null);

        await cancellation.CancelAsync();
        await host;
    }

    [Test]
    public async Task A_start_with_migration_off_does_not_stamp_the_target()
    {
        await SeedCheckpoint(MigrationCategoryIds.KnownEndpoints);
        SetSourceVariable("SERVICECONTROL_MIGRATION_ENABLED", "false");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await RunHostUntilTheApiAnswers(SignallingOnceStarted(started));
        await started.Task.WaitAsync(TimeSpan.FromMinutes(1));

        Assert.That(await ReadSetting(HostOpenedSetting), Is.Null, "only a start with the migration on may write the marker");
    }

    [Test]
    public async Task A_start_with_migration_off_does_not_read_the_checkpoint_table()
    {
        await DropTheCheckpointTable();
        SetSourceVariable("SERVICECONTROL_MIGRATION_ENABLED", "false");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await RunHostUntilTheApiAnswers(SignallingOnceStarted(started));

        Assert.That(async () => await started.Task.WaitAsync(TimeSpan.FromMinutes(1)), Throws.Nothing, "every hosted service, StartedAsync included, ran without touching the missing table");
    }

    [Test]
    public async Task An_ingestion_only_host_let_in_by_AllowIncompleteExit_records_the_row()
    {
        Settings.MaximumConcurrencyLevel = 1;
        SetSourceVariable("SERVICECONTROL_MIGRATION_ENABLED", "false");
        SetSourceVariable("SERVICECONTROL_MIGRATION_ALLOWINCOMPLETEEXIT", "true");
        await SeedCheckpoint(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.InProgress);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var app = ErrorIngestionOnlyCommand.BuildHost(Settings, AllowingAnUnreleasedMigration());

        await app.StartAsync(cancellation.Token);

        try
        {
            Assert.That(await ReadSetting(HostOpenedSetting), Is.Not.Null, "the worker ingests into a database whose copy is unfinished, so the abort is no longer free");
        }
        finally
        {
            await app.StopAsync(cancellation.Token);
            await app.DisposeAsync();
        }
    }

    [Test]
    public async Task Importing_failed_errors_is_refused_while_a_copy_is_unfinished()
    {
        SetSourceVariable("SERVICECONTROL_MIGRATION_ENABLED", "false");
        await SeedCheckpoint(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.InProgress);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        using var host = ImportFailedErrorsCommand.BuildHost(Settings);

        var refusal = Assert.ThrowsAsync<Exception>(async () => await host.StartAsync(cancellation.Token));

        Assert.That(refusal.Message, Does.Contain("--import-failed-errors will not start").And.Contain("KnownEndpoints is InProgress"));
    }

    [Test]
    public async Task Importing_failed_errors_let_in_by_AllowIncompleteExit_records_the_row()
    {
        SetSourceVariable("SERVICECONTROL_MIGRATION_ENABLED", "false");
        SetSourceVariable("SERVICECONTROL_MIGRATION_ALLOWINCOMPLETEEXIT", "true");
        await SeedCheckpoint(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.InProgress);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        using var host = ImportFailedErrorsCommand.BuildHost(Settings);

        await host.StartAsync(cancellation.Token);

        try
        {
            Assert.That(await ReadSetting(HostOpenedSetting), Is.Not.Null, "importing into a database whose copy is unfinished ends the free abort");
        }
        finally
        {
            await host.StopAsync(cancellation.Token);
        }
    }

    // A customer whose migration never started must not be told they have passed the point of no return.
    [Test]
    public async Task The_row_does_not_exist_after_a_startup_check_refused()
    {
        // Seeded so the marker would be written if anything stamped it, which makes the refusal the only reason it is not.
        await SeedCheckpoint(MigrationCategoryIds.KnownEndpoints);
        SetSourceVariable("SERVICECONTROL_MIGRATION_EVENTLOGWINDOW", "NoSuchWindow");

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        Assert.That(async () => await RunCommand.Run(Settings, AllowingAnUnreleasedMigration(), cancellation.Token),
            Throws.Exception.With.Message.Contains("NoSuchWindow"));

        Assert.That(await ReadSetting(HostOpenedSetting), Is.Null, "a refused startup has opened on nothing");
    }

    // Ingestion-only hosts write failed messages into the same database a migration targets, so a rollback
    // gate that had not seen them would discard everything they ingested.
    [Test]
    public async Task An_ingestion_only_host_records_the_row_too()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        // The ingestion-only host runs a real transport, which the migration fixture does not otherwise configure.
        Settings.MaximumConcurrencyLevel = 1;

        await SeedCheckpoint(MigrationCategoryIds.KnownEndpoints);

        var app = ErrorIngestionOnlyCommand.BuildHost(Settings, AllowingAnUnreleasedMigration());

        await app.StartAsync(cancellation.Token);

        try
        {
            Assert.That(await ReadSetting(HostOpenedSetting), Is.Not.Null, "only RunCommand used to record this, so a scaled-out ingestion host wrote to the target and left no trace of having opened on it");
        }
        finally
        {
            await app.StopAsync(cancellation.Token);
            await app.DisposeAsync();
        }
    }


    // A customer running on SQL who has never migrated must not be told the way back is gone.
    [Test]
    public async Task A_database_no_migration_has_touched_is_not_stamped()
    {
        Settings.MaximumConcurrencyLevel = 1;

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var app = ErrorIngestionOnlyCommand.BuildHost(Settings, AllowingAnUnreleasedMigration());

        await app.StartAsync(cancellation.Token);

        try
        {
            Assert.That(await ReadSetting(HostOpenedSetting), Is.Null, "no checkpoint row exists, so no migration has ever run against this database");
        }
        finally
        {
            await app.StopAsync(cancellation.Token);
            await app.DisposeAsync();
        }
    }

    // ApplicationStarted fires only after every hosted service's StartedAsync, which is where the marker is written.
    static Action<WebApplicationBuilder> SignallingOnceStarted(TaskCompletionSource started) =>
        builder => builder.Services.AddHostedService(provider => new SignalWhenStarted(provider.GetRequiredService<IHostApplicationLifetime>(), started));

    sealed class SignalWhenStarted(IHostApplicationLifetime lifetime, TaskCompletionSource started) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            lifetime.ApplicationStarted.Register(() => started.TrySetResult());
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    Task<int> DropTheCheckpointTable() =>
        QueryTarget(dbContext =>
        {
            var table = dbContext.Model.FindEntityType(typeof(MigrationCheckpointEntity))!;
            var name = dbContext.GetService<ISqlGenerationHelper>().DelimitIdentifier(table.GetTableName()!, table.GetSchema());

            // The name comes from the EF model and the provider delimits it, so no outside input reaches this SQL.
#pragma warning disable EF1003
            return dbContext.Database.ExecuteSqlRawAsync("DROP TABLE " + name);
#pragma warning restore EF1003
        });
}
