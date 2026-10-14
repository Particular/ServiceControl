namespace ServiceControl.Migration.AcceptanceTests;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.Tests;

// Proves a copy larger than one batch, where one batch is as many rows as one insert statement takes.
[TestFixture]
[NonParallelizable]
class When_a_batch_exceeds_one_statement : MigrationAcceptanceTest
{
    [Test]
    public async Task Copies_more_messages_than_fit_in_one_statement()
    {
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);
        var batchSize = await Target.BatchSizeFor(category);
        var failures = Enumerable.Range(0, (batchSize * 2) + 1).Select(_ => new IngestedFailure()).ToArray();

        await SeedSourceWithBodies([.. failures.Select(failure => (failure.ToFailedMessage(), failure.Body, failure.ContentType))]);

        var result = await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        Assert.That(result.Copied, Is.EqualTo(failures.Length));

        foreach (var failure in failures)
        {
            Assert.That(await FindFailedMessage(failure.UniqueMessageId), Is.Not.Null);
        }
    }
}
