namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration.Writers;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.MessageRedirects;

class MessageRedirectsMigrationTargetTests : PersistenceTestBase
{
    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    static readonly MigrationCategory MessageRedirectsCategory = MigrationCategoryRegistry.Find(MigrationCategoryIds.MessageRedirects)!;

    [Test]
    public async Task A_redirect_with_an_address_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy()
    {
        var batch = BatchOf(
            ("Redirect/1", Redirect(new string('f', 450), new string('t', 450))),
            ("Redirect/2", Redirect(new string('f', 451), "Sales@NEWHOST")),
            ("Redirect/3", Redirect("Billing@OLDHOST", new string('t', 451))));

        var details = await PreparedSkips.DetailsFrom(ServiceProvider, new MessageRedirectsWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>()), batch);
        var result = await Target.Write(MessageRedirectsCategory, batch, CheckpointAfter("Redirect/3"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1), "a value exactly as long as its column fits");
            Assert.That(result.SkippedRows.Select(row => row.SourceId), Is.EqualTo(new[] { "Redirect/2", "Redirect/3" }));
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.ValueTooLong] = 2 }), "an over-long value would fail the whole insert and halt the category");
            Assert.That((await MessageRedirectsDataStore.GetRedirects()).Select(redirect => redirect.FromPhysicalAddress), Is.EqualTo(new[] { new string('f', 450) }));
            Assert.That(details, Has.Count.EqualTo(2));
            Assert.That(details[0], Does.EndWith("takes at most 450 characters and the value holds 451, on document 'Redirect/2'"));
            Assert.That(details[1], Does.EndWith("takes at most 450 characters and the value holds 451, on document 'Redirect/3'"));
        }
    }

    [Test]
    public async Task A_redirect_whose_address_the_key_treats_as_an_earlier_ones_is_reported_as_a_merge()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var batch = BatchOf(
            ("Redirect/1", Redirect("Sales@HOST", "Sales@NEWHOST")),
            ("Redirect/2", Redirect("Sales@HOST", "Sales@OTHERHOST")),
            ("Redirect/3", Redirect("sales@host", "Sales@NEWHOST")));
        var keyIgnoresCase = dialect.KeyComparer(typeof(MessageRedirectEntity), nameof(MessageRedirectEntity.FromPhysicalAddress)).Equals("Sales@HOST", "sales@host");

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, new MessageRedirectsWriter(dialect), batch);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Select(merge => merge.SourceId), Is.EqualTo(keyIgnoresCase ? new[] { "Redirect/2", "Redirect/3" } : new[] { "Redirect/2" }),
                "a second copy of an address is dropped on every database, and a case-only variant only where the key column ignores case");
            Assert.That(merges[0].Detail, Does.Contain("kept 'Sales@HOST'"), "the target logs this line, so it has to name the address it kept");
        }
    }

    static MessageRedirect Redirect(string from, string to) =>
        new() { FromPhysicalAddress = from, ToPhysicalAddress = to, LastModified = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc) };

    static MigrationBatch BatchOf(params (string Id, object Document)[] rows) =>
        new([.. rows.Select(row => new MigrationRow(row.Id, row.Document, new Dictionary<string, object>()))], rows[^1].Id);

    static MigrationCheckpoint CheckpointAfter(string cursor) =>
        new(MigrationCategoryIds.MessageRedirects, MigrationCategoryState.InProgress, cursor, 0, 0, null, null, null, null, null, null);
}
