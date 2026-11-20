namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration.Writers;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

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

        Assert.That(target.SupportedCategoryIds, Is.EquivalentTo(ValueTooLongTests.Keys.Concat(NoValueCanBeTooLong)),
            "a writer that does not run the length check sends an over-long value to the insert, which fails the whole statement and halts the category; give the new category a ValueTooLong test like those listed here, then add it to this list");
    }

    [Test]
    public async Task The_settings_value_column_has_no_maximum_length_so_no_setting_can_be_too_long()
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model;

        Assert.That(model.FindEntityType(typeof(SettingEntity))!.FindProperty(nameof(SettingEntity.Value))!.GetMaxLength(), Is.Null, "NoValueCanBeTooLong exempts the settings categories because of this; give them a value-too-long test if the column gains a limit");
    }

    [TestCase(typeof(CustomCheckEntity))]
    [TestCase(typeof(FailedErrorImportEntity))]
    public async Task No_text_column_of_the_entity_has_a_maximum_length_so_no_value_can_be_too_long(Type entity)
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var entityType = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model.FindEntityType(entity)!;

        Assert.That(
            entityType.GetProperties().Where(property => property.ClrType == typeof(string) && property.GetMaxLength() is not null).Select(property => $"{entityType.GetTableName()}.{property.GetColumnName()}"),
            Is.Empty,
            "NoValueCanBeTooLong exempts this entity's category because of this; give it a value-too-long test if a column gains a limit");
    }

    [Test]
    public async Task Every_writer_whose_rows_a_re_read_cannot_recognise_deletes_them_for_a_retry_and_no_other_writer_does()
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model;
        var writers = typeof(IMigrationCategoryWriter).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IMigrationCategoryWriter).IsAssignableFrom(type));

        Assert.That(writers, Is.EquivalentTo(EntitiesEachWriterWrites.Keys), "list the entities a new writer writes here, so this test can tell whether a retry must delete its rows");

        using (Assert.EnterMultipleScope())
        {
            foreach (var (writer, entities) in EntitiesEachWriterWrites)
            {
                var storeKeyed = entities.Any(entity => model.FindEntityType(entity)!.FindPrimaryKey()!.Properties.Any(property => property.ValueGenerated != ValueGenerated.Never));

                Assert.That(typeof(IStoreKeyedCategoryWriter).IsAssignableFrom(writer), Is.EqualTo(storeKeyed || WritersThatAddToRowsTheyCopied.Contains(writer)),
                    $"{writer.Name}: a retry keeps the rows of a writer that is not an IStoreKeyedCategoryWriter, so a re-read adds rows with store keys or added counts a second time, and a writer whose rows a re-read finds already present must not delete");
            }
        }
    }

    // None of these copies a source value into a text column with a maximum length, which the two no-maximum-length tests above pin.
    static readonly string[] NoValueCanBeTooLong = [MigrationCategoryIds.LicensingReportMasks, MigrationCategoryIds.LicensedEndpointDetails, MigrationCategoryIds.NotificationSettings, MigrationCategoryIds.TrialEndDate, MigrationCategoryIds.CustomChecks, MigrationCategoryIds.FailedErrorImports];

    // Uses nameof, so renaming or deleting one of these tests fails the build instead of leaving this list out of date.
    static readonly Dictionary<string, string> ValueTooLongTests = new()
    {
        [MigrationCategoryIds.KnownEndpoints] = nameof(KnownEndpointsMigrationTargetTests.A_known_endpoint_with_a_value_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy),
        [MigrationCategoryIds.EndpointSettings] = nameof(EndpointSettingsMigrationTargetTests.A_setting_whose_name_is_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy),
        [MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages] = nameof(FailedMessagesMigrationTargetTests.A_message_or_group_value_longer_than_its_column_skips_the_message_whole_and_the_rest_copy),
        [MigrationCategoryIds.LicensingEndpoints] = nameof(LicensingEndpointsMigrationTargetTests.A_licensing_endpoint_with_a_value_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy),
        [MigrationCategoryIds.LicensingThroughput] = nameof(LicensingThroughputMigrationTargetTests.A_day_whose_endpoint_name_is_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy),
        [MigrationCategoryIds.MessageRedirects] = nameof(MessageRedirectsMigrationTargetTests.A_redirect_with_an_address_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy),
        [MigrationCategoryIds.Subscriptions] = nameof(SubscriptionsMigrationTargetTests.A_subscription_with_a_value_longer_than_its_column_is_skipped_under_its_reason_and_the_rest_copy),
        [MigrationCategoryIds.RetryOperations] = nameof(RetryOperationsMigrationTargetTests.A_retry_operation_with_a_value_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy),
        [MigrationCategoryIds.PendingIntegrationEvents] = nameof(PendingIntegrationEventsMigrationTargetTests.A_request_whose_context_type_name_is_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy),
        [MigrationCategoryIds.GroupComments] = nameof(GroupCommentsMigrationTargetTests.A_comment_whose_group_id_is_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy)
    };

    // Their keys come from the source, but a re-read adds its counts to the stored ones, so a re-read cannot find them already present.
    static readonly Type[] WritersThatAddToRowsTheyCopied = [typeof(LicensingThroughputWriter)];

    // The writers do not say which entities they write, so this list does.
    static readonly Dictionary<Type, Type[]> EntitiesEachWriterWrites = new()
    {
        [typeof(KnownEndpointsWriter)] = [typeof(KnownEndpointEntity)],
        [typeof(EndpointSettingsWriter)] = [typeof(EndpointSettingsEntity)],
        [typeof(FailedMessagesWriter)] = [typeof(FailedMessageEntity), typeof(FailedMessageGroupEntity)],
        [typeof(LicensingEndpointsWriter)] = [typeof(LicensingEndpointEntity)],
        [typeof(LicensingThroughputWriter)] = [typeof(LicensingEndpointThroughputEntity)],
        [typeof(LicensingReportMasksWriter)] = [typeof(SettingEntity)],
        [typeof(LicensedEndpointDetailsWriter)] = [typeof(SettingEntity)],
        [typeof(MessageRedirectsWriter)] = [typeof(MessageRedirectEntity)],
        [typeof(SubscriptionsWriter)] = [typeof(SubscriptionEntity)],
        [typeof(NotificationSettingsWriter)] = [typeof(SettingEntity)],
        [typeof(TrialEndDateWriter)] = [typeof(SettingEntity)],
        [typeof(RetryOperationsWriter)] = [typeof(HistoricRetryOperationEntity), typeof(UnacknowledgedRetryOperationEntity)],
        [typeof(PendingIntegrationEventsWriter)] = [typeof(ExternalIntegrationDispatchRequestEntity)],
        [typeof(CustomChecksWriter)] = [typeof(CustomCheckEntity)],
        [typeof(FailedErrorImportsWriter)] = [typeof(FailedErrorImportEntity)],
        [typeof(GroupCommentsWriter)] = [typeof(GroupCommentEntity)]
    };
}
