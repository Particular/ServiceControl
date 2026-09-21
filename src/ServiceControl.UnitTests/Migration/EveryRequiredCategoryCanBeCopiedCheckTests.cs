namespace ServiceControl.UnitTests.Migration;

using System;
using System.Linq;
using NUnit.Framework;
using ServiceControl.Migration;
using ServiceControl.Migration.Checks;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class EveryRequiredCategoryCanBeCopiedCheckTests
{
    static readonly string[] EveryRequiredCategory =
        [.. MigrationCategoryRegistry.All.Where(category => category.Kind == MigrationCategoryKind.Required).Select(category => category.Id)];

    [Test]
    public void A_build_that_cannot_copy_the_whole_required_set_refuses_MigrationEnabled()
    {
        var copyable = EveryRequiredCategory.Except([MigrationCategoryIds.RetryOperations]).ToArray();

        var exception = Assert.ThrowsAsync<Exception>(() => new EveryRequiredCategoryCanBeCopiedCheck(copyable).Run());

        Assert.That(exception.Message, Does.Contain(MigrationCategoryIds.RetryOperations).And.Contain(MigrationSettings.EnabledKey),
            "a customer who cannot see which categories are missing, or which setting stranded them, has nothing to act on");
    }

    [Test]
    public void The_acceptance_fixtures_marker_lets_the_copy_this_build_can_do_run()
    {
        Assert.DoesNotThrowAsync(() => new EveryRequiredCategoryCanBeCopiedCheck([], new AllowIncompleteCategorySet()).Run());
    }

    [Test]
    public void A_build_that_copies_every_required_category_passes_without_the_marker()
    {
        Assert.DoesNotThrowAsync(() => new EveryRequiredCategoryCanBeCopiedCheck(EveryRequiredCategory).Run());
    }

    [Test]
    public void A_category_only_one_side_supports_counts_as_not_copyable()
    {
        var source = new[] { MigrationCategoryIds.KnownEndpoints, MigrationCategoryIds.EndpointSettings };
        var target = new[] { MigrationCategoryIds.KnownEndpoints };

        Assert.That(MigrationStartup.CopyableCategoryIds(source, target), Is.EquivalentTo(new[] { MigrationCategoryIds.KnownEndpoints }),
            "a category with a reader and no writer would otherwise be attempted and fail partway through a customer's copy");
    }
}
