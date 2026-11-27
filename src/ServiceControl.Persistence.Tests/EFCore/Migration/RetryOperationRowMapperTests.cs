namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Recoverability;

class RetryOperationRowMapperTests : PersistenceTestBase
{
    static readonly DateTime CompletedAt = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Sets_every_mapped_property_of_a_historic_and_an_unacknowledged_operation()
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model;

        using (Assert.EnterMultipleScope())
        {
            MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(model, RetryOperationRowMapper.Map(new HistoricRetryOperation
            {
                RequestId = "group-1",
                RetryType = RetryType.FailureGroup,
                StartTime = CompletedAt.AddMinutes(-1),
                CompletionTime = CompletedAt,
                Originator = "Sales.Orders",
                Failed = true,
                NumberOfMessagesProcessed = 5
            }));
            MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(model, RetryOperationRowMapper.Map(new UnacknowledgedRetryOperation
            {
                RequestId = "group-2",
                RetryType = RetryType.FailureGroup,
                StartTime = CompletedAt.AddMinutes(-1),
                CompletionTime = CompletedAt,
                Last = CompletedAt,
                Originator = "Shipping",
                Classifier = "Shipping-Failures",
                Failed = true,
                NumberOfMessagesProcessed = 2
            }));
        }
    }
}
