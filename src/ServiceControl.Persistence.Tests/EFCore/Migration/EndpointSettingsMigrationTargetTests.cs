namespace ServiceControl.Persistence.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
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
    public async Task A_setting_whose_endpoint_is_not_known_is_copied_and_left_to_the_heartbeat_sync()
    {
        var result = await Target.Write(
            EndpointSettingsCategory,
            BatchOf(
                ("EndpointSettings/1", new EndpointSettings { Name = string.Empty, TrackInstances = true }),
                ("EndpointSettings/2", new EndpointSettings { Name = "Retired", TrackInstances = false })),
            CheckpointAfter("EndpointSettings/2"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(2));
            Assert.That(result.Skipped, Is.Zero, "whether a setting outlives its endpoint is the heartbeat sync's rule, not the migration's");
            Assert.That(await TrackInstancesFor(string.Empty), Is.True);
            Assert.That(await TrackInstancesFor("Retired"), Is.False);
        }
    }

    [Test]
    public async Task A_batch_that_copies_nothing_still_saves_the_cursor()
    {
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Sales", TrackInstances = true });
        await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = "Billing", TrackInstances = true });

        var result = await Target.Write(
            EndpointSettingsCategory,
            BatchOf(
                ("EndpointSettings/1", new EndpointSettings { Name = "Sales", TrackInstances = false }),
                ("EndpointSettings/2", new EndpointSettings { Name = "Billing", TrackInstances = false })),
            CheckpointAfter("EndpointSettings/2"));

        var stored = await CheckpointStore.Read(MigrationCategoryIds.EndpointSettings);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.Zero);
            Assert.That(result.AlreadyPresent, Is.EqualTo(2));
            Assert.That(stored.Cursor, Is.EqualTo("EndpointSettings/2"), "a batch that copied nothing still has to commit the cursor past it, or the restart reads the same rows forever");
        }
    }

    [Test]
    public async Task Every_mapped_column_is_set_from_a_fully_populated_document()
    {
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

    [Test]
    public async Task Two_names_in_one_batch_differing_only_in_case_are_each_copied_or_already_present()
    {
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
