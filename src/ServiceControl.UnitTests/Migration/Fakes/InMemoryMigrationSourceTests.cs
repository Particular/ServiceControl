#nullable enable
namespace ServiceControl.UnitTests.Migration.Fakes;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class InMemoryMigrationSourceTests
{
    static MigrationRow Row(string id) => new(id, new object(), new Dictionary<string, object?>());

    [Test]
    public async Task Streams_seeded_rows_in_batches_of_the_requested_size()
    {
        var source = new InMemoryMigrationSource();
        source.Seed("EndpointSettings", Row("a"), Row("b"), Row("c"), Row("d"), Row("e"));

        var batches = new List<MigrationBatch>();
        await foreach (var batch in source.Read(MigrationCategoryRegistry.Find("EndpointSettings")!, resumeAfter: null, batchSize: 2))
        {
            batches.Add(batch);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batches, Has.Count.EqualTo(3));
            Assert.That(batches[0].Rows.Select(r => r.SourceId), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(batches[2].Rows.Select(r => r.SourceId), Is.EqualTo(new[] { "e" }));
        }
    }

    [Test]
    public async Task ResumeAfter_skips_everything_up_to_and_including_that_id()
    {
        var source = new InMemoryMigrationSource();
        source.Seed("EndpointSettings", Row("a"), Row("b"), Row("c"));

        var batches = new List<MigrationBatch>();
        await foreach (var batch in source.Read(MigrationCategoryRegistry.Find("EndpointSettings")!, resumeAfter: "a", batchSize: 10))
        {
            batches.Add(batch);
        }

        Assert.That(batches.Single().Rows.Select(r => r.SourceId), Is.EqualTo(new[] { "b", "c" }));
    }

    [Test]
    public void A_cursor_the_source_never_issued_throws_rather_than_starting_again()
    {
        var source = new InMemoryMigrationSource();
        source.Seed("EndpointSettings", Row("a"), Row("b"));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in source.Read(MigrationCategoryRegistry.Find("EndpointSettings")!, resumeAfter: "no-such-row", batchSize: 10))
            {
            }
        });
    }

    [Test]
    public async Task ReadBody_returns_a_queued_attempt_then_falls_back_to_the_seeded_body()
    {
        var source = new InMemoryMigrationSource();
        var finalBody = new MigrationBody(new byte[] { 9 }, "text/plain");
        source.SetBody("msg-1", finalBody);
        source.QueueBodyAttempt("msg-1", () => throw new System.Exception("transient"));

        Assert.ThrowsAsync<System.Exception>(() => source.ReadBody(MigrationCategoryRegistry.Find(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages)!, "msg-1"));
        var secondAttempt = await source.ReadBody(MigrationCategoryRegistry.Find(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages)!, "msg-1");

        Assert.That(secondAttempt, Is.EqualTo(finalBody));
    }
}
