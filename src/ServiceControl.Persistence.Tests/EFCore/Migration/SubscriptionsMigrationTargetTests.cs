namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration.Writers;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class SubscriptionsMigrationTargetTests : PersistenceTestBase
{
    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    static readonly MigrationCategory SubscriptionsCategory = MigrationCategoryRegistry.Find(MigrationCategoryIds.Subscriptions);

    [Test]
    public async Task A_subscription_with_a_value_longer_than_its_column_is_skipped_under_its_reason_and_the_rest_copy()
    {
        var version = new Version(1, 0, 0);
        var batch = BatchOf(
            ("s/1", new SubscriptionRow(new string('t', 200), version, new string('a', 200), new string('e', 450))),
            ("s/2", new SubscriptionRow(new string('t', 201), version, "Billing@BILLING01", "Billing")),
            ("s/3", new SubscriptionRow("MyMessages.OrderPlaced", version, new string('a', 201), "Billing")),
            ("s/4", new SubscriptionRow("MyMessages.OrderPlaced", version, "Shipping@SHIP01", new string('e', 451))));

        var details = await PreparedSkips.DetailsFrom(ServiceProvider, new SubscriptionsWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>()), batch);
        var result = await Target.Write(SubscriptionsCategory, batch, CheckpointAfter("s/4"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1), "a value exactly as long as its column fits");
            Assert.That(result.SkippedRows.Select(row => row.SourceId), Is.EqualTo(new[] { "s/2", "s/3", "s/4" }));
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.ValueTooLong] = 3 }),
                "an over-long value would fail the whole insert and halt the category");
            Assert.That(details, Is.EqualTo(new[]
            {
                $"{PreparedSkips.TableAndColumnOf<SubscriptionEntity>(ServiceProvider, nameof(SubscriptionEntity.MessageType))} takes at most 200 characters and the value holds 201, on document 's/2'",
                $"{PreparedSkips.TableAndColumnOf<SubscriptionEntity>(ServiceProvider, nameof(SubscriptionEntity.TransportAddress))} takes at most 200 characters and the value holds 201, on document 's/3'",
                $"{PreparedSkips.TableAndColumnOf<SubscriptionEntity>(ServiceProvider, nameof(SubscriptionEntity.Endpoint))} takes at most 450 characters and the value holds 451, on document 's/4'"
            }));
            Assert.That(await StoredSubscriptions(), Is.EqualTo(new[] { (new string('t', 200), new string('a', 200), new string('e', 450)) }), "the row whose values all fit is stored as it was read");
            Assert.That(await Target.Count(SubscriptionsCategory), Is.EqualTo(1), "Count is the rows the Subscriptions table holds");
        }
    }

    [Test]
    public async Task A_second_version_of_a_type_for_one_subscriber_is_reported_as_a_merge()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var batch = BatchOf(
            ("s/1", new SubscriptionRow("MyMessages.OrderPlaced", new Version(1, 0, 0), "Billing@BILLING01", "Billing")),
            ("s/2", new SubscriptionRow("MyMessages.OrderPlaced", new Version(2, 0, 0), "Billing@BILLING01", "Billing")),
            ("s/3", new SubscriptionRow("MyMessages.OrderPlaced", new Version(1, 0, 0), "billing@billing01", "Billing")),
            ("s/4", new SubscriptionRow("MyMessages.OrderShipped", new Version(1, 0, 0), "Billing@BILLING01", "Billing")));
        var addressIgnoresCase = dialect.KeyComparer(typeof(SubscriptionEntity), nameof(SubscriptionEntity.TransportAddress)).Equals("Billing@BILLING01", "billing@billing01");

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, new SubscriptionsWriter(dialect), batch);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Select(merge => merge.SourceId), Is.EqualTo(addressIgnoresCase ? new[] { "s/2", "s/3" } : new[] { "s/2" }),
                "the key has no version, so a second version is dropped on every database, a case-only address only where the column ignores case, and another type never");
            Assert.That(merges[0].Detail, Does.Contain("kept 'MyMessages.OrderPlaced', 'Billing@BILLING01'"), "the target logs this line, so it has to name the key it kept");

            if (addressIgnoresCase)
            {
                Assert.That(merges[1].Detail, Does.Contain("kept 'MyMessages.OrderPlaced', 'Billing@BILLING01' and dropped the row for 'MyMessages.OrderPlaced', 'billing@billing01'"),
                    "the line must not swap the key kept and the key dropped");
            }
        }
    }

    [Test]
    public async Task A_merged_subscription_names_the_version_and_endpoint_of_the_row_kept_and_the_row_dropped()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var batch = BatchOf(
            ("s/1", new SubscriptionRow("MyMessages.OrderPlaced", new Version(1, 0, 0), "Billing@BILLING01", "Billing")),
            ("s/2", new SubscriptionRow("MyMessages.OrderPlaced", new Version(2, 0, 0), "Billing@BILLING01", "Billing.V2")));

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, new SubscriptionsWriter(dialect), batch);
        var result = await Target.Write(SubscriptionsCategory, batch, CheckpointAfter("s/2"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Single().Detail, Does.EndWith("It kept the row from 's/1' (version 1.0.0, endpoint 'Billing') and dropped the row from 's/2' (version 2.0.0, endpoint 'Billing.V2')."),
                "both keys print the same, so only the source rows tell the operator which version survived");
            Assert.That((result.AlreadyPresent, result.Merged), Is.EqualTo((1, 1)), "the summary counts the merge apart from the other rows already present");
        }
    }

    [Test]
    public async Task A_subscription_merged_on_a_re_read_names_the_row_an_earlier_pass_copied()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var kept = ("s/1", new SubscriptionRow("MyMessages.OrderPlaced", new Version(1, 0, 0), "Billing@BILLING01", "Billing"));
        var first = await Target.Write(SubscriptionsCategory, BatchOf(kept), CheckpointAfter("s/1"));
        var reRead = BatchOf(kept, ("s/2", new SubscriptionRow("MyMessages.OrderPlaced", new Version(2, 0, 0), "Billing@BILLING01", "Billing")));

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, new SubscriptionsWriter(dialect), reRead);
        var result = await Target.Write(SubscriptionsCategory, reRead, first.Saved);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Single().Detail, Does.EndWith("It kept the row from 's/1' (version 1.0.0, endpoint 'Billing') and dropped the row from 's/2' (version 2.0.0, endpoint 'Billing')."),
                "the stored row is the one 's/1' copied on the earlier pass, so the line names that source row");
            Assert.That((result.AlreadyPresent, result.Merged), Is.EqualTo((2, 1)));
        }
    }

    [Test]
    public async Task A_key_the_target_already_holds_merges_only_a_variant_the_column_treats_as_the_same()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var addressIgnoresCase = dialect.KeyComparer(typeof(SubscriptionEntity), nameof(SubscriptionEntity.TransportAddress)).Equals("Billing@BILLING01", "billing@billing01");
        var first = await Target.Write(SubscriptionsCategory, BatchOf(("s/1", new SubscriptionRow("MyMessages.OrderPlaced", new Version(1, 0, 0), "Billing@BILLING01", "Billing"))), CheckpointAfter("s/1"));

        // The case variant comes first, so on a case-insensitive column only the stored row can be what it merges with.
        var second = BatchOf(
            ("s/2", new SubscriptionRow("MyMessages.OrderPlaced", new Version(1, 0, 0), "billing@billing01", "Billing")),
            ("s/3", new SubscriptionRow("MyMessages.OrderPlaced", new Version(1, 0, 0), "Billing@BILLING01", "Billing")),
            ("s/4", new SubscriptionRow("MyMessages.OrderPlaced", new Version(1, 0, 0), "Shipping@SHIP01", "Shipping")));

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, new SubscriptionsWriter(dialect), second);
        var result = await Target.Write(SubscriptionsCategory, second, first.Saved with { Cursor = second.Cursor });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Select(merge => merge.SourceId), Is.EqualTo(addressIgnoresCase ? new[] { "s/2" } : Array.Empty<string>()),
                "an exact repeat of a stored key is an earlier pass's row, a case variant merges only where the column ignores case, and another address never");

            if (addressIgnoresCase)
            {
                Assert.That(merges[0].Detail, Does.Contain("kept 'MyMessages.OrderPlaced', 'Billing@BILLING01' and dropped the row for 'MyMessages.OrderPlaced', 'billing@billing01'"));
                Assert.That(merges[0].Detail, Does.EndWith("It kept the row the table already held and dropped the row from 's/2' (version 1.0.0, endpoint 'Billing')."), "no row of this batch claimed the stored row");
            }

            Assert.That(result.Copied, Is.EqualTo(addressIgnoresCase ? 1 : 2), "the other address is copied, and so is the case variant where the column tells it apart");
            Assert.That(result.AlreadyPresent, Is.EqualTo(addressIgnoresCase ? 2 : 1));
            Assert.That(await StoredSubscriptions(), Is.EquivalentTo(addressIgnoresCase
                ? new[] { ("MyMessages.OrderPlaced", "Billing@BILLING01", "Billing"), ("MyMessages.OrderPlaced", "Shipping@SHIP01", "Shipping") }
                : new[] { ("MyMessages.OrderPlaced", "Billing@BILLING01", "Billing"), ("MyMessages.OrderPlaced", "billing@billing01", "Billing"), ("MyMessages.OrderPlaced", "Shipping@SHIP01", "Shipping") }));
        }
    }

    [Test]
    public async Task A_subscription_missing_a_key_column_is_skipped_naming_the_column_and_the_rest_copy()
    {
        var version = new Version(1, 0, 0);
        var batch = BatchOf(
            ("s/1", new SubscriptionRow("MyMessages.OrderPlaced", version, TransportAddress: null!, "Billing")),
            ("s/2", new SubscriptionRow(MessageType: null!, version, "Billing@BILLING01", "Billing")),
            ("s/3", new SubscriptionRow("MyMessages.OrderPlaced", version, "Shipping@SHIP01", "Shipping")));

        var details = await PreparedSkips.DetailsFrom(ServiceProvider, new SubscriptionsWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>()), batch);
        var result = await Target.Write(SubscriptionsCategory, batch, CheckpointAfter("s/3"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(details, Is.EqualTo(new[]
            {
                "Subscriptions.TransportAddress is NOT NULL and the document has no value for it",
                "Subscriptions.MessageType is NOT NULL and the document has no value for it"
            }), "the target logs this line, so it has to name the column that is missing");
            Assert.That(result.SkippedRows.Select(row => row.SourceId), Is.EqualTo(new[] { "s/1", "s/2" }));
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = 2 }));
            Assert.That(await StoredSubscriptions(), Is.EqualTo(new[] { ("MyMessages.OrderPlaced", "Shipping@SHIP01", "Shipping") }));
        }
    }

    async Task<IReadOnlyList<(string MessageType, string TransportAddress, string Endpoint)>> StoredSubscriptions()
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Subscriptions.AsNoTracking().ToListAsync();

        return [.. stored.Select(subscription => (subscription.MessageType, subscription.TransportAddress, subscription.Endpoint))];
    }

    static MigrationBatch BatchOf(params (string Id, SubscriptionRow Document)[] rows) =>
        new([.. rows.Select(row => new MigrationRow(row.Id, row.Document, new Dictionary<string, object>()))], rows[^1].Id);

    static MigrationCheckpoint CheckpointAfter(string cursor) =>
        new(MigrationCategoryIds.Subscriptions, MigrationCategoryState.InProgress, cursor, 0, 0, null, null, null, null, null, null);
}
