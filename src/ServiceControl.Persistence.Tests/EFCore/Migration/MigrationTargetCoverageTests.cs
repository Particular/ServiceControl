namespace ServiceControl.Persistence.Tests;

using System.Collections.Generic;
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

        Assert.That(target.SupportedCategoryIds, Is.SupersetOf(new[] { MigrationCategoryIds.KnownEndpoints, MigrationCategoryIds.EndpointSettings, MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages }), "the test proves nothing if the target supports no category");

        foreach (var id in target.SupportedCategoryIds)
        {
            var category = MigrationCategoryRegistry.Find(id)!;

            Assert.That(await target.BatchSizeFor(category, CancellationToken.None), Is.Positive, $"{id}: every supported category has a batch size");
            Assert.That(await target.Count(category, CancellationToken.None), Is.Zero, id);
        }
    }

    [Test]
    public void Every_category_the_target_supports_has_a_test_for_a_value_longer_than_its_column()
    {
        var target = ServiceProvider.GetRequiredService<IMigrationTarget>();

        Assert.That(target.SupportedCategoryIds, Is.EquivalentTo(ValueTooLongTests.Keys),
            "a writer that does not run the length check sends an over-long value to the insert, which fails the whole statement and halts the category; give the new category a ValueTooLong test like those listed here, then add it to this list");
    }

    // Uses nameof, so renaming or deleting one of these tests fails the build instead of leaving this list out of date.
    static readonly Dictionary<string, string> ValueTooLongTests = new()
    {
        [MigrationCategoryIds.KnownEndpoints] = nameof(KnownEndpointsMigrationTargetTests.A_known_endpoint_with_a_value_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy),
        [MigrationCategoryIds.EndpointSettings] = nameof(EndpointSettingsMigrationTargetTests.A_setting_whose_name_is_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy),
        [MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages] = nameof(FailedMessagesMigrationTargetTests.A_message_or_group_value_longer_than_its_column_skips_the_message_whole_and_the_rest_copy)
    };
}
