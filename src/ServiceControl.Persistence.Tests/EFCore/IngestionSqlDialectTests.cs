namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class IngestionSqlDialectTests : ErrorIngestionTestBase
{
    [Test]
    public async Task Writes_every_mapped_column_of_a_failed_message()
    {
        var expected = FailedMessageWithEveryPropertySet();

        await Upsert(expected);

        var stored = await FindFailedMessage(expected.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            foreach (var property in MappedProperties())
            {
                Assert.That(property.GetValue(stored), Is.EqualTo(property.GetValue(expected)), $"{property.Name} did not survive the round trip");
            }
        }
    }

    static FailedMessageEntity FailedMessageWithEveryPropertySet()
    {
        var now = new DateTime(2026, 8, 3, 9, 30, 0, DateTimeKind.Utc);

        return new FailedMessageEntity
        {
            UniqueMessageId = Guid.NewGuid(),
            Status = FailedMessageStatus.Archived,
            StatusChangedAt = now,
            LastModified = now.AddMinutes(1),
            NumberOfProcessingAttempts = 7,
            FirstTimeOfFailure = now.AddMinutes(2),
            LastTimeOfFailure = now.AddMinutes(3),
            LastAttemptedAt = now.AddMinutes(4),
            MessageId = "message-id",
            MessageType = "MyCompany.Sales.OrderPlaced",
            TimeSent = now.AddMinutes(5),
            ConversationId = "conversation-id",
            SendingEndpointName = "Ordering",
            SendingEndpointHostId = Guid.NewGuid(),
            SendingEndpointHost = "SenderHost",
            ReceivingEndpointName = "Sales",
            ReceivingEndpointHostId = Guid.NewGuid(),
            ReceivingEndpointHost = "ReceiverHost",
            ExceptionType = "System.InvalidOperationException",
            ExceptionMessage = "Something went wrong",
            IsSystemMessage = true,
            HeadersJson = """{"NServiceBus.MessageId":"message-id"}""",
            BodyText = "<order>1</order>",
            BodyStoredExternally = true,
            BodySize = 16,
            BodyContentType = "text/xml",
            FailingEndpointAddress = "Sales@MACHINE"
        };
    }

    System.Reflection.PropertyInfo[] MappedProperties()
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        return [.. dbContext.Model
            .FindEntityType(typeof(FailedMessageEntity))!
            .GetProperties()
            .Select(property => property.PropertyInfo)
            .Where(property => property != null)
            .Select(property => property!)];
    }

    async Task Upsert(FailedMessageEntity row)
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var dialect = scope.ServiceProvider.GetRequiredService<IIngestionSqlDialect>();

        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            await dialect.UpsertFailedMessages(dbContext, [row], TestContext.CurrentContext.CancellationToken);

            await transaction.CommitAsync();
        });
    }
}
