#nullable enable
namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;

class FailedMessageMigrationCoverageTests : PersistenceTestBase
{
    [Test]
    public void Every_failed_message_column_is_set_from_a_fully_populated_document()
    {
        var source = FullyPopulatedRow();
        var row = ((FailedMessageRowMapping.Mapped)FailedMessageRowMapper.Map(source)).Row;
        // Over the cap, so it sets all four body columns: an inline prefix, the external flag, the size and the content type.
        var overCapText = Encoding.UTF8.GetBytes(new string('a', BodyStorageSettings.DefaultMaxBodySizeToStore * 2));

        FailedMessageRowMapper.SetBody(row, ((FailedMessage)source.Document).ProcessingAttempts[^1].Headers, new MigrationBody(overCapText, "text/plain"), BodyStorageSettings.DefaultMaxBodySizeToStore);

        MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(Model(), row);
    }

    [Test]
    public void Every_failure_group_column_is_set_from_a_fully_populated_document()
    {
        var mapping = (FailedMessageRowMapping.Mapped)FailedMessageRowMapper.Map(FullyPopulatedRow());

        MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(Model(), mapping.Groups.Single());
    }

    static MigrationRow FullyPopulatedRow()
    {
        var failure = new IngestedFailure { IsSystemMessage = true, FailingEndpointAddress = "Sales@MACHINE" };

        return new MigrationRow(
            $"FailedMessages/{failure.UniqueMessageIdString}",
            failure.ToFailedMessage(),
            new Dictionary<string, object?>
            {
                [MigrationMetadataKeys.StatusChangedAt] = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
                [MigrationMetadataKeys.LastModified] = new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc)
            });
    }

    IModel Model()
    {
        using var scope = ServiceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model;
    }
}
