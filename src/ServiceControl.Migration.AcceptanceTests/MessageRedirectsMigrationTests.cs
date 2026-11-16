namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.MessageRedirects;

[TestFixture]
[NonParallelizable]
class MessageRedirectsMigrationTests : MigrationAcceptanceTest
{
    [Test]
    public async Task Copies_three_redirects_from_one_document_into_three_rows()
    {
        var seeded = new[]
        {
            new MessageRedirect { FromPhysicalAddress = "old1", ToPhysicalAddress = "new1", LastModified = new DateTime(2026, 3, 1, 12, 0, 1, DateTimeKind.Utc) },
            new MessageRedirect { FromPhysicalAddress = "old2", ToPhysicalAddress = "new2", LastModified = new DateTime(2026, 3, 1, 12, 0, 2, DateTimeKind.Utc) },
            new MessageRedirect { FromPhysicalAddress = "old3", ToPhysicalAddress = "new3", LastModified = new DateTime(2026, 3, 1, 12, 0, 3, DateTimeKind.Utc) }
        };
        await SeedSourceMessageRedirects(seeded);

        var result = await CopyCategory("MessageRedirects");

        Assert.That(result.Copied, Is.EqualTo(3));

        var redirects = await MessageRedirectsDataStore.GetRedirects();
        Assert.That(redirects.Select(r => (r.FromPhysicalAddress, r.ToPhysicalAddress, r.LastModified)),
            Is.EquivalentTo(seeded.Select(r => (r.FromPhysicalAddress, r.ToPhysicalAddress, r.LastModified))),
            "each redirect keeps its own destination and the time it was last changed");
        Assert.That(await Target.Count(MigrationCategoryRegistry.Find(MigrationCategoryIds.MessageRedirects)!), Is.EqualTo(3), "--migration-verify reads this count");
    }

    [TestCase("old1", "old1")]
    [TestCase("Sales@HOST", "sales@host")]
    public async Task A_repeated_address_is_copied_or_already_present_as_the_database_compares_it(string firstAddress, string secondAddress)
    {
        await SeedSourceMessageRedirects(
            new MessageRedirect { FromPhysicalAddress = firstAddress, ToPhysicalAddress = "new1", LastModified = DateTime.UtcNow },
            new MessageRedirect { FromPhysicalAddress = secondAddress, ToPhysicalAddress = "new2", LastModified = DateTime.UtcNow });

        var result = await CopyCategory("MessageRedirects");
        var stored = await MessageRedirectsDataStore.GetRedirects();

        Assert.That(result.Skipped, Is.Zero, "a repeat loses no row the target lacks, so it is never a skip");
        Assert.That(result.Copied + result.AlreadyPresent, Is.EqualTo(2));
        Assert.That(result.Copied, Is.EqualTo(stored.Count),
            "Copied is the rows the database stored: one where the key treats the addresses as the same, two where it does not");
        Assert.That(stored.Select(redirect => (redirect.FromPhysicalAddress, redirect.ToPhysicalAddress)), Does.Contain((firstAddress, "new1")),
            "the first redirect in document order is copied on every database");
    }

    [Test]
    public async Task A_redirect_with_no_destination_is_skipped_as_a_required_value_missing()
    {
        await SeedSourceMessageRedirects(
            new MessageRedirect { FromPhysicalAddress = "old1", ToPhysicalAddress = null!, LastModified = DateTime.UtcNow },
            new MessageRedirect { FromPhysicalAddress = "old2", ToPhysicalAddress = "new2", LastModified = DateTime.UtcNow },
            new MessageRedirect { FromPhysicalAddress = null!, ToPhysicalAddress = "new3", LastModified = DateTime.UtcNow });

        var result = await CopyCategory("MessageRedirects");
        Assert.That(result.Saved.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors), "one fault skip ends a required category Failed");
        Assert.That(result.SkippedIds, Is.EqualTo(new[] { "old1", "messageredirects#2" }), "a redirect with no source address is named by its place in the document");

        var checkpoint = await ReadCheckpoint("MessageRedirects");
        Assert.That(checkpoint.CopiedCount, Is.EqualTo(1), "a skip copies the rest of the batch, where a throw would have stopped it");
        Assert.That(checkpoint.SkippedCount, Is.EqualTo(2));
        Assert.That(checkpoint.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.RequiredValueMissing] = 2 }));
    }
}
