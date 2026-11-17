namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

class FailedErrorImportRowMapperTests : PersistenceTestBase
{
    [Test]
    public async Task Sets_every_mapped_property_of_a_failed_error_import()
    {
        var import = new FailedErrorImport
        {
            Id = "native-1",
            Message = new FailedTransportMessage { Id = "native-1", Headers = new Dictionary<string, string> { ["NServiceBus.MessageId"] = "native-1" }, Body = [1, 2, 3] },
            ExceptionInfo = "Deserialization failed"
        };
        var failedAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

        await using var scope = ServiceProvider.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model;

        // Inline leaves the flag false and external leaves the column empty, so each call covers the one the other cannot.
        MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(model, FailedErrorImportRowMapper.Map(import, failedAt, bodyStoredExternally: false), nameof(FailedErrorImportEntity.BodyStoredExternally));
        MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(model, FailedErrorImportRowMapper.Map(import, failedAt, bodyStoredExternally: true), nameof(FailedErrorImportEntity.Body));
    }
}
