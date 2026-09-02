namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class FailedMessageIngestionSqlDialectTests : ErrorIngestionTestBase
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

    // Every one of these row counts fills at least one whole statement, which is where a chunk
    // size that lands on the database's parameter ceiling instead of under it gets rejected.

    [Test]
    public async Task Upserts_more_messages_than_fit_in_one_statement()
    {
        var rows = Enumerable.Range(0, 200).Select(_ => MinimalFailedMessage()).ToArray();

        await InDialectTransaction((dialect, dbContext, ct) => dialect.UpsertFailedMessages(dbContext, rows, ct));

        Assert.That(await CountStored(rows.Select(row => row.UniqueMessageId)), Is.EqualTo(rows.Length));
    }

    [Test]
    public async Task Inserts_more_groups_than_fit_in_one_statement()
    {
        var messages = Enumerable.Range(0, 600).Select(_ => MinimalFailedMessage()).ToArray();
        await InDialectTransaction((dialect, dbContext, ct) => dialect.UpsertFailedMessages(dbContext, messages, ct));

        var rows = messages.Select(message => new FailedMessageGroupEntity
        {
            FailedMessageUniqueId = message.UniqueMessageId,
            GroupId = Guid.NewGuid().ToString(),
            Title = "a group",
            Type = "ExceptionType"
        }).ToArray();

        await InDialectTransaction((dialect, dbContext, ct) => dialect.InsertGroups(dbContext, rows, ct));

        Assert.That(await CountGroups(rows.Select(row => row.FailedMessageUniqueId)), Is.EqualTo(rows.Length));
    }

    [Test]
    public async Task Inserts_more_known_endpoints_than_fit_in_one_statement()
    {
        var rows = Enumerable.Range(0, 500).Select(_ => new KnownEndpointEntity
        {
            Id = Guid.NewGuid(),
            Name = "Sales",
            HostId = Guid.NewGuid(),
            Host = "SalesHost",
            Monitored = false
        }).ToArray();

        await InDialectTransaction((dialect, dbContext, ct) => dialect.InsertMissingKnownEndpoints(dbContext, rows, ct));

        Assert.That(await CountEndpoints(rows.Select(row => row.Id)), Is.EqualTo(rows.Length));
    }

    [Test]
    public async Task Resolves_more_retried_messages_than_fit_in_one_statement()
    {
        var rows = Enumerable.Range(0, 1100).Select(_ => MinimalFailedMessage()).ToArray();
        await InDialectTransaction((dialect, dbContext, ct) => dialect.UpsertFailedMessages(dbContext, rows, ct));

        var succeededAt = rows[0].LastAttemptedAt.AddMinutes(1);
        var retries = rows.Select(row => new ConfirmedRetry(row.UniqueMessageId, succeededAt)).ToArray();

        await InDialectTransaction((dialect, dbContext, ct) => dialect.ResolveRetriedMessages(dbContext, retries, succeededAt, ct));

        Assert.That(await CountResolved(rows.Select(row => row.UniqueMessageId)), Is.EqualTo(rows.Length));
    }

    static FailedMessageEntity MinimalFailedMessage()
    {
        var now = new DateTime(2026, 8, 3, 9, 30, 0, DateTimeKind.Utc);

        return new FailedMessageEntity
        {
            UniqueMessageId = Guid.NewGuid(),
            Status = FailedMessageStatus.Unresolved,
            StatusChangedAt = now,
            LastModified = now,
            NumberOfProcessingAttempts = 1,
            FirstTimeOfFailure = now,
            LastTimeOfFailure = now,
            LastAttemptedAt = now,
            MessageId = "message-id",
            HeadersJson = "{}",
            FailingEndpointAddress = "Sales@MACHINE"
        };
    }

    async Task<int> CountStored(IEnumerable<Guid> ids)
    {
        var wanted = ids.ToHashSet();
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        return await dbContext.FailedMessages.CountAsync(row => wanted.Contains(row.UniqueMessageId));
    }

    async Task<int> CountResolved(IEnumerable<Guid> ids)
    {
        var wanted = ids.ToHashSet();
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        return await dbContext.FailedMessages.CountAsync(row => wanted.Contains(row.UniqueMessageId) && row.Status == FailedMessageStatus.Resolved);
    }

    async Task<int> CountGroups(IEnumerable<Guid> ids)
    {
        var wanted = ids.ToHashSet();
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        return await dbContext.FailedMessageGroups.CountAsync(row => wanted.Contains(row.FailedMessageUniqueId));
    }

    async Task<int> CountEndpoints(IEnumerable<Guid> ids)
    {
        var wanted = ids.ToHashSet();
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        return await dbContext.KnownEndpoints.CountAsync(row => wanted.Contains(row.Id));
    }

    Task Upsert(FailedMessageEntity row) =>
        InDialectTransaction((dialect, dbContext, ct) => dialect.UpsertFailedMessages(dbContext, [row], ct));

    async Task InDialectTransaction(Func<IFailedMessageIngestionSqlDialect, ServiceControlDbContext, CancellationToken, Task> work)
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var dialect = scope.ServiceProvider.GetRequiredService<IFailedMessageIngestionSqlDialect>();

        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            await work(dialect, dbContext, TestContext.CurrentContext.CancellationToken);

            await transaction.CommitAsync();
        });
    }
}
