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
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;

class KnownEndpointsMigrationTargetTests : PersistenceTestBase
{
    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    static readonly MigrationCategory KnownEndpointsCategory = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.KnownEndpoints);

    [Test]
    public async Task A_known_endpoint_is_written_with_its_monitored_flag()
    {
        var endpoint = new KnownEndpoint
        {
            EndpointDetails = new EndpointDetails { Name = "Sales.Orders", HostId = Guid.NewGuid(), Host = "SALES01" },
            HostDisplayName = "SALES01",
            Monitored = true
        };
        var sourceId = $"KnownEndpoints/{endpoint.EndpointDetails.GetDeterministicId()}";

        var result = await Target.Write(KnownEndpointsCategory, BatchOf((sourceId, endpoint)), CheckpointAfter(sourceId));

        var stored = (await MonitoringDataStore.GetAllKnownEndpoints()).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(stored.Monitored, Is.True);
            Assert.That(stored.EndpointDetails.Name, Is.EqualTo("Sales.Orders"));
        }
    }

    [Test]
    public async Task A_known_endpoint_already_in_the_target_keeps_its_flag_and_is_counted_as_already_present()
    {
        var details = new EndpointDetails { Name = "Sales.Orders", HostId = Guid.NewGuid(), Host = "SALES01" };
        await MonitoringDataStore.CreateIfNotExists(details);

        var endpoint = new KnownEndpoint { EndpointDetails = details, HostDisplayName = "SALES01", Monitored = true };
        var sourceId = $"KnownEndpoints/{details.GetDeterministicId()}";

        var result = await Target.Write(KnownEndpointsCategory, BatchOf((sourceId, endpoint)), CheckpointAfter(sourceId));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.Zero);
            Assert.That(result.AlreadyPresent, Is.EqualTo(1));
            Assert.That((await MonitoringDataStore.GetAllKnownEndpoints()).Single().Monitored, Is.False, "insert-if-absent never updates, so the flag the target already had stays");
        }
    }

    [Test]
    public async Task A_known_endpoint_with_no_name_or_no_host_is_skipped_as_required_value_missing()
    {
        var named = new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = "Sales.Orders", HostId = Guid.NewGuid(), Host = "SALES01" } };
        var nameless = new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = null, HostId = Guid.NewGuid(), Host = "SALES02" } };
        var hostless = new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = "Billing", HostId = Guid.NewGuid(), Host = null } };

        var result = await Target.Write(KnownEndpointsCategory, BatchOf(("KnownEndpoints/1", named), ("KnownEndpoints/2", nameless), ("KnownEndpoints/3", hostless)), CheckpointAfter("KnownEndpoints/3"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.SkippedIds, Is.EquivalentTo(new[] { "KnownEndpoints/2", "KnownEndpoints/3" }));
            Assert.That(result.SkipReasons[MigrationSkipReason.RequiredValueMissing], Is.EqualTo(2), "KnownEndpoints.Name and Host are NOT NULL, and a throw here would halt a required category");
            Assert.That((await MonitoringDataStore.GetAllKnownEndpoints()).Select(endpoint => endpoint.EndpointDetails.Name), Is.EqualTo(new[] { "Sales.Orders" }));
        }
    }

    [Test]
    public async Task A_batch_of_nothing_but_skips_saves_the_cursor_and_writes_no_rows()
    {
        var nameless = new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = null, HostId = Guid.NewGuid(), Host = "SALES02" } };
        var hostless = new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = "Billing", HostId = Guid.NewGuid(), Host = null } };

        var result = await Target.Write(KnownEndpointsCategory, BatchOf(("KnownEndpoints/1", nameless), ("KnownEndpoints/2", hostless)), CheckpointAfter("KnownEndpoints/2"));

        var stored = await ServiceProvider.GetRequiredService<IMigrationCheckpointStore>().Read(MigrationCategoryIds.KnownEndpoints);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.Zero);
            Assert.That(result.AlreadyPresent, Is.Zero, "no key was looked up, so no row may be counted as one the target already held");
            Assert.That(result.Skipped, Is.EqualTo(2));
            Assert.That(result.BenignSkipped, Is.Zero, "a missing NOT NULL column is a fault, and faults have to reach the halt threshold");
            Assert.That(await MonitoringDataStore.GetAllKnownEndpoints(), Is.Empty);
            Assert.That(stored.Cursor, Is.EqualTo("KnownEndpoints/2"), "a batch that copied nothing still has to commit the cursor past it, or the restart reads the same rows forever");
        }
    }

    [Test]
    public void A_batch_reporting_more_copied_and_skipped_rows_than_it_held_is_refused()
    {
        // The guard only counts rows, so what the documents hold cannot change its answer.
        var batch = BatchOf(("KnownEndpoints/1", new object()), ("KnownEndpoints/2", new object()));

        var exception = Assert.Throws<InvalidOperationException>(() => EFCoreMigrationTarget.AlreadyPresentIn(KnownEndpointsCategory, batch, copied: 2, skipped: 1));

        Assert.That(exception.Message, Does.Contain("copied 2").And.Contain("skipped 1").And.Contain(MigrationCategoryIds.KnownEndpoints));
    }

    [Test]
    public async Task A_batch_whose_checkpoint_save_fails_leaves_no_rows_behind()
    {
        // The rows save first and the checkpoint second, so this is the only order in which the two can part
        // company: a stale version fails the checkpoint after the endpoint row is already in the transaction.
        var store = ServiceProvider.GetRequiredService<IMigrationCheckpointStore>();
        await store.Upsert(CheckpointAfter("KnownEndpoints/0"));

        var endpoint = new KnownEndpoint
        {
            EndpointDetails = new EndpointDetails { Name = "Sales.Orders", HostId = Guid.NewGuid(), Host = "SALES01" },
            Monitored = true
        };
        var sourceId = $"KnownEndpoints/{endpoint.EndpointDetails.GetDeterministicId()}";

        Assert.ThrowsAsync<MigrationCheckpointConflictException>(async () =>
            await Target.Write(KnownEndpointsCategory, BatchOf((sourceId, endpoint)), CheckpointAfter(sourceId)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await MonitoringDataStore.GetAllKnownEndpoints(), Is.Empty, "the rows and the checkpoint commit together, so a failed checkpoint takes the rows with it");
            Assert.That((await store.Read(MigrationCategoryIds.KnownEndpoints)).Cursor, Is.EqualTo("KnownEndpoints/0"), "the stored cursor must still describe the rows the target actually holds");
        }
    }

    [Test]
    public async Task Every_mapped_column_is_set_from_a_fully_populated_document()
    {
        var endpoint = new KnownEndpoint
        {
            EndpointDetails = new EndpointDetails { Name = "Sales.Orders", HostId = Guid.NewGuid(), Host = "SALES01" },
            HostDisplayName = "SALES01",
            Monitored = true
        };
        var sourceId = $"KnownEndpoints/{endpoint.EndpointDetails.GetDeterministicId()}";

        await Target.Write(KnownEndpointsCategory, BatchOf((sourceId, endpoint)), CheckpointAfter(sourceId));

        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(dbContext.Model, await dbContext.KnownEndpoints.AsNoTracking().SingleAsync());
    }


    [Test]
    public async Task A_batch_spanning_several_statements_counts_what_each_statement_inserted()
    {
        var rows = Enumerable.Range(0, 500)
            .Select(index => new KnownEndpoint { EndpointDetails = new EndpointDetails { Name = $"Endpoint{index}", HostId = Guid.NewGuid(), Host = "HOST01" }, Monitored = index % 2 == 0 })
            .Select(endpoint => ($"KnownEndpoints/{endpoint.EndpointDetails.GetDeterministicId()}", (object)endpoint))
            .ToArray();
        var batch = BatchOf(rows);

        var first = await Target.Write(KnownEndpointsCategory, batch, CheckpointAfter(batch.Cursor));
        // The engine carries the committed checkpoint into the next write, and the version guard refuses anything else.
        var second = await Target.Write(KnownEndpointsCategory, batch, first.Saved with { Cursor = batch.Cursor });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Copied, Is.EqualTo(500));
            Assert.That(second.Copied, Is.Zero, "every key is present on the second write, so no statement may report a row it did not insert");
            Assert.That(second.AlreadyPresent, Is.EqualTo(500));
        }
    }


    [Test]
    public void A_batch_whose_writer_neither_prepared_nor_skipped_a_row_is_refused()
    {
        // The already-present count is a subtraction, so it only catches a writer that over-reports. A row dropped in silence under-reports, and without this guard it is counted as a row the target already held.
        var batch = BatchOf(("KnownEndpoints/1", new object()), ("KnownEndpoints/2", new object()));
        var prepared = new PreparedBatch((_, _) => Task.FromResult(1), PreparedRowCount: 1, Skips: []);

        var exception = Assert.Throws<InvalidOperationException>(() => EFCoreMigrationTarget.AccountForEveryRow(KnownEndpointsCategory, batch, prepared));

        Assert.That(exception.Message, Does.Contain("prepared 1").And.Contain("skipped 0").And.Contain(MigrationCategoryIds.KnownEndpoints));
    }

    static MigrationBatch BatchOf(params (string Id, object Document)[] rows) =>
        new([.. rows.Select(row => new MigrationRow(row.Id, row.Document, new Dictionary<string, object>()))], rows[^1].Id);

    static MigrationCheckpoint CheckpointAfter(string cursor) =>
        new(MigrationCategoryIds.KnownEndpoints, MigrationCategoryState.InProgress, cursor, 0, 0, null, null, null, null, null, null);
}
