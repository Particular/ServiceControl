#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class MigrationEngineCollisionTests
{
    static MigrationRow Row(string id, string name) => new(id, new { Name = name }, new Dictionary<string, object?>());

    [Test]
    public async Task Rows_already_present_in_the_target_are_counted_as_already_present_across_batches_not_skipped_or_overwritten()
    {
        var category = MigrationCategoryRegistry.Find("UnresolvedAndRetryIssuedFailedMessages")!;
        var source = new InMemoryMigrationSource();
        source.Seed(category.Id, Row("msg-1", "from-source"), Row("msg-2", "from-source"), Row("msg-3", "from-source"), Row("msg-4", "from-source"));
        var checkpointStore = new InMemoryMigrationCheckpointStore();
        var target = new InMemoryMigrationTarget(checkpointStore) { DefaultBatchSize = 3 };
        // msg-1 and msg-4 already exist in the target: they failed again after cutover, and real ingestion wrote them.
        target.SeedExistingKey("msg-1");
        target.SeedExistingKey("msg-4");
        var options = new MigrationEngineOptions(TimeSpan.Zero, 5, 100, []);
        var engine = new MigrationEngine(source, target, checkpointStore, new FakeTimeProvider(), options, NullLogger<MigrationEngine>.Instance);

        var checkpoint = await engine.RunCategoryAsync(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(checkpoint.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(checkpoint.CopiedCount, Is.EqualTo(2));
            Assert.That(checkpoint.SkippedCount, Is.Zero);
            Assert.That(checkpoint.AlreadyPresentCount, Is.EqualTo(2));
            // Asserted through the fake's recorded rows, never by the engine inspecting a document.
            Assert.That(target.WrittenRows(category.Id).Select(r => r.SourceId), Is.EqualTo(new[] { "msg-2", "msg-3" }));
        }
    }
}
