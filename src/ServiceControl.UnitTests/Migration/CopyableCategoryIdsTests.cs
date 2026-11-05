namespace ServiceControl.UnitTests.Migration;

using NUnit.Framework;
using ServiceControl.Migration;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class CopyableCategoryIdsTests
{
    [Test]
    public void A_category_only_one_side_supports_counts_as_not_copyable()
    {
        var source = new[] { MigrationCategoryIds.KnownEndpoints, MigrationCategoryIds.EndpointSettings };
        var target = new[] { MigrationCategoryIds.KnownEndpoints };

        Assert.That(MigrationStartup.CopyableCategoryIds(source, target), Is.EquivalentTo(new[] { MigrationCategoryIds.KnownEndpoints }),
            "a category with a reader and no writer would otherwise be attempted and fail partway through a customer's copy");
    }
}
