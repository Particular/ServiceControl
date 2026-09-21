namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;

class EndpointSettingsMigrationTargetTests : PersistenceTestBase
{
    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    IMigrationCheckpointStore CheckpointStore => ServiceProvider.GetRequiredService<IMigrationCheckpointStore>();

    static readonly MigrationCategory EndpointSettingsCategory = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.EndpointSettings);

    [Test]
    public async Task Every_setting_in_a_batch_arrives_with_its_track_instances_value()
    {
        await SeedKnownEndpoints("Sales", "Billing", "Shipping");

        var result = await Target.Write(
            EndpointSettingsCategory,
            BatchOf(
                ("EndpointSettings/1", new EndpointSettings { Name = "Sales", TrackInstances = true }),
                ("EndpointSettings/2", new EndpointSettings { Name = "Billing", TrackInstances = false }),
                ("EndpointSettings/3", new EndpointSettings { Name = "Shipping", TrackInstances = true })),
            CheckpointAfter("EndpointSettings/3"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(3), "Copied is what the insert added, not the size of the batch");
            Assert.That(await TrackInstancesFor("Sales"), Is.True);
            Assert.That(await TrackInstancesFor("Billing"), Is.False);
            Assert.That(await TrackInstancesFor("Shipping"), Is.True);
        }
    }

    [Test]
    public async Task A_name_already_in_the_target_is_left_alone_and_counted_as_already_present()
    {
        await SeedKnownEndpoints("Sales");
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Sales", TrackInstances = true });

        var result = await Target.Write(
            EndpointSettingsCategory,
            BatchOf(("EndpointSettings/7", new EndpointSettings { Name = "Sales", TrackInstances = false })),
            CheckpointAfter("EndpointSettings/7"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.Zero);
            Assert.That(result.AlreadyPresent, Is.EqualTo(1));
            Assert.That(result.Skipped, Is.Zero, "a row the target already holds lost nothing, so it is not a skip");
            Assert.That(await TrackInstancesFor("Sales"), Is.True);
        }
    }

    [Test]
    public async Task A_named_setting_whose_endpoint_is_not_known_is_skipped_as_endpoint_not_known()
    {
        await SeedKnownEndpoints("Sales");

        var result = await Target.Write(
            EndpointSettingsCategory,
            BatchOf(
                ("EndpointSettings/1", new EndpointSettings { Name = "Retired", TrackInstances = true }),
                ("EndpointSettings/2", new EndpointSettings { Name = "Sales", TrackInstances = true })),
            CheckpointAfter("EndpointSettings/2"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { "EndpointSettings/1" }));
            Assert.That(result.SkipReasons[MigrationSkipReason.EndpointNotKnown], Is.EqualTo(1), "the heartbeat settings sync deletes this row twenty seconds after the host opens, so verify has to see it as a skip");
            Assert.That(result.BenignSkipped, Is.EqualTo(1), "a setting left behind on purpose is reported, but a source full of them must not halt a required category");
            Assert.That((await EndpointSettingsStore.GetAllEndpointSettings().ToListAsync()).Select(settings => settings.Name), Is.EqualTo(new[] { "Sales" }));
        }
    }

    [Test]
    public async Task An_unknown_endpoint_stays_a_benign_skip_while_known_endpoints_dropped_nothing()
    {
        await SeedKnownEndpoints("Sales");
        await SaveKnownEndpointsCheckpoint(skipped: 0);

        var result = await Target.Write(
            EndpointSettingsCategory,
            BatchOf(("EndpointSettings/1", new EndpointSettings { Name = "Retired", TrackInstances = true })),
            CheckpointAfter("EndpointSettings/1"));

        Assert.That(result.BenignSkipped, Is.EqualTo(1), "the endpoint is unknown in the source too, so the sync would delete this setting whatever the migration did");
    }

    [Test]
    public async Task An_unknown_endpoint_stops_being_a_benign_skip_once_known_endpoints_dropped_a_row()
    {
        await SeedKnownEndpoints("Sales");
        await SaveKnownEndpointsCheckpoint(skipped: 1);

        var result = await Target.Write(
            EndpointSettingsCategory,
            BatchOf(("EndpointSettings/1", new EndpointSettings { Name = "Retired", TrackInstances = true })),
            CheckpointAfter("EndpointSettings/1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Skipped, Is.EqualTo(1));
            Assert.That(result.BenignSkipped, Is.Zero, "the endpoint may be unknown only because KnownEndpoints dropped it, and a setting the sync would have kept has to reach the halt threshold");
        }
    }

    [Test]
    public async Task A_batch_of_nothing_but_skips_saves_the_cursor_and_writes_no_rows()
    {
        var result = await Target.Write(
            EndpointSettingsCategory,
            BatchOf(
                ("EndpointSettings/1", new EndpointSettings { Name = "Retired", TrackInstances = true }),
                ("EndpointSettings/2", new EndpointSettings { Name = "Decommissioned", TrackInstances = false })),
            CheckpointAfter("EndpointSettings/2"));

        var stored = await CheckpointStore.Read(MigrationCategoryIds.EndpointSettings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.Zero);
            Assert.That(result.AlreadyPresent, Is.Zero, "no name was looked up, so no row may be counted as one the target already held");
            Assert.That(result.Skipped, Is.EqualTo(2));
            Assert.That(await EndpointSettingsStore.GetAllEndpointSettings().ToListAsync(), Is.Empty);
            Assert.That(stored.Cursor, Is.EqualTo("EndpointSettings/2"), "a batch that copied nothing still has to commit the cursor past it, or the restart reads the same rows forever");
        }
    }

    [Test]
    public async Task The_empty_name_default_is_copied_when_no_endpoint_is_known()
    {
        var result = await Target.Write(EndpointSettingsCategory, BatchOf(("EndpointSettings/1", new EndpointSettings { Name = string.Empty, TrackInstances = true })), CheckpointAfter("EndpointSettings/1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.Skipped, Is.Zero, "the sync keeps the default whatever endpoints are known");
            Assert.That(await TrackInstancesFor(string.Empty), Is.True);
        }
    }

    [Test]
    public async Task Every_mapped_column_is_set_from_a_fully_populated_document()
    {
        await SeedKnownEndpoints("Sales");
        await Target.Write(EndpointSettingsCategory, BatchOf(("EndpointSettings/1", new EndpointSettings { Name = "Sales", TrackInstances = true })), CheckpointAfter("EndpointSettings/1"));

        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(dbContext.Model, await dbContext.EndpointSettings.AsNoTracking().SingleAsync());
    }

    static MigrationBatch BatchOf(params (string Id, object Document)[] rows) =>
        new([.. rows.Select(row => new MigrationRow(row.Id, row.Document, new Dictionary<string, object>()))], rows[^1].Id);

    static MigrationCheckpoint CheckpointAfter(string cursor) =>
        new(MigrationCategoryIds.EndpointSettings, MigrationCategoryState.InProgress, cursor, 0, 0, null, null, null, null, null, null);

    async Task<bool> TrackInstancesFor(string name) =>
        (await EndpointSettingsStore.GetAllEndpointSettings().ToListAsync()).Single(settings => settings.Name == name).TrackInstances;

    // The target reads this checkpoint to tell an endpoint the source never had from one KnownEndpoints dropped.
    Task SaveKnownEndpointsCheckpoint(long skipped) =>
        CheckpointStore.Upsert(new MigrationCheckpoint(
            MigrationCategoryIds.KnownEndpoints,
            skipped == 0 ? MigrationCategoryState.Complete : MigrationCategoryState.CompleteWithErrors,
            "KnownEndpoints/9",
            CopiedCount: 9,
            SkippedCount: skipped,
            SourceTotal: null,
            SkipReasons: skipped == 0 ? null : new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = skipped },
            StartedAt: null,
            LastProgressAt: null,
            SettledAt: null,
            LastError: null));

    // The target copies a named setting only when its endpoint is known, because the heartbeat settings sync keeps only those.
    async Task SeedKnownEndpoints(params string[] names)
    {
        foreach (var name in names)
        {
            await MonitoringDataStore.CreateIfNotExists(new EndpointDetails { Name = name, HostId = Guid.NewGuid(), Host = "HOST01" });
        }
    }

    [Test]
    public async Task Two_names_in_one_batch_differing_only_in_case_are_each_copied_or_already_present()
    {
        await SeedKnownEndpoints("Sales", "sales");

        var result = await Target.Write(
            EndpointSettingsCategory,
            BatchOf(
                ("EndpointSettings/1", new EndpointSettings { Name = "Sales", TrackInstances = true }),
                ("EndpointSettings/2", new EndpointSettings { Name = "sales", TrackInstances = false })),
            CheckpointAfter("EndpointSettings/2"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied + result.AlreadyPresent, Is.EqualTo(2), "SQL Server's default collation makes these one key and PostgreSQL makes them two; either way neither row may throw or go uncounted");
            Assert.That(result.Skipped, Is.Zero, "a merge onto a row the batch writes loses nothing the target lacks, and a skip would count toward the halt threshold");
            Assert.That(await Target.Count(EndpointSettingsCategory), Is.EqualTo(result.Copied), "every row reported as copied is a row in the table");
            Assert.That(await TrackInstancesFor("Sales"), Is.True, "the first row in document-id order wins where the key makes the two one");
        }
    }

}
