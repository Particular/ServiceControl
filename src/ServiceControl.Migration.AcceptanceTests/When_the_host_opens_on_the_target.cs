namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
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
        var host = RunCommand.Run(Settings, AllowingAnIncompleteCategorySet(builder => builder.ParkFirstMigrationWrite(copyIsParked, releaseTheCopy.Task)), cancellation.Token);

        await copyIsParked.Task.WaitAsync(TimeSpan.FromMinutes(2));
        Assert.That(await ReadSetting(HostOpenedSetting), Is.Null, "the abort is still free at this moment, and this row is what says it is not");

        releaseTheCopy.SetResult();
        await WaitForEndpointSettingsResponse(TimeSpan.FromMinutes(2));

        Assert.That(await ReadSetting(HostOpenedSetting), Is.Not.Null);

        await cancellation.CancelAsync();
        await host;
    }

    // A customer whose migration never started must not be told they have passed the point of no return.
    [Test]
    public async Task The_row_does_not_exist_after_a_startup_check_refused()
    {
        SetSourceVariable("SERVICECONTROL_MIGRATION_OPTIONALCATEGORIES", "NoSuchCategory");

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        Assert.That(async () => await RunCommand.Run(Settings, AllowingAnIncompleteCategorySet(), cancellation.Token),
            Throws.Exception.With.Message.Contains("NoSuchCategory"));

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

        var app = ErrorIngestionOnlyCommand.BuildHost(Settings, AllowingAnIncompleteCategorySet());

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
        var app = ErrorIngestionOnlyCommand.BuildHost(Settings, AllowingAnIncompleteCategorySet());

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

}
