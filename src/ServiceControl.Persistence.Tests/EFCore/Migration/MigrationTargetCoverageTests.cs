namespace ServiceControl.Persistence.Tests;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

class MigrationTargetCoverageTests : PersistenceTestBase
{
    [Test]
    public async Task Every_category_the_target_supports_has_a_batch_size_and_a_count()
    {
        var target = ServiceProvider.GetRequiredService<IMigrationTarget>();

        Assert.That(target.SupportedCategoryIds, Is.SupersetOf(new[] { MigrationCategoryIds.KnownEndpoints, MigrationCategoryIds.EndpointSettings }), "the test proves nothing if the target supports no category");

        foreach (var id in target.SupportedCategoryIds)
        {
            var category = MigrationCategoryRegistry.Find(id)!;

            Assert.That(target.BatchSizeFor(category), Is.Positive, $"{id}: every supported category has a batch size");
            Assert.That(await target.Count(category, CancellationToken.None), Is.Zero, id);
        }
    }
}
