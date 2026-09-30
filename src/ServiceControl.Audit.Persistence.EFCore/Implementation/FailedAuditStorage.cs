namespace ServiceControl.Audit.Persistence.EFCore.Implementation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Audit.Auditing;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.DbContexts;
using ServiceControl.Audit.Persistence.EFCore.Entities;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
using ServiceControl.Audit.Persistence.Infrastructure;

sealed class FailedAuditStorage(IServiceScopeFactory scopeFactory, EFPersisterSettings settings, TimeProvider timeProvider)
    : DataStoreBase(scopeFactory, settings), IFailedAuditStorage
{
    const int BatchSize = 100;

    public Task SaveFailedAuditImport(FailedAuditImport message, CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            var key = Key(message.Id);
            var failedAt = timeProvider.GetUtcNow().UtcDateTime;
            var headersJson = MessageHeaders.Write(message.Message?.Headers ?? []);
            var body = message.Message?.Body ?? [];

            await dbContext.UpsertAsync<FailedAuditImportEntity>(key, () => new FailedAuditImportEntity
            {
                UniqueMessageId = key,
                FailedAt = failedAt,
                MessageId = message.Message?.Id,
                HeadersJson = headersJson,
                Body = body,
                ExceptionInfo = message.ExceptionInfo
            }, entity =>
            {
                entity.FailedAt = failedAt;
                entity.MessageId = message.Message?.Id;
                entity.HeadersJson = headersJson;
                entity.Body = body;
                entity.ExceptionInfo = message.ExceptionInfo;
            }, token);
        }, cancellationToken);

    public async Task ProcessFailedMessages(
        Func<FailedTransportMessage, Func<CancellationToken, Task>, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken = default)
    {
        var completed = new List<Guid>();

        for (var offset = 0; ; offset += BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = await ExecuteWithDbContext((dbContext, token) => dbContext.FailedAuditImports
                .AsNoTracking()
                .OrderBy(import => import.FailedAt)
                .ThenBy(import => import.UniqueMessageId)
                .Skip(offset)
                .Take(BatchSize)
                .ToListAsync(token), cancellationToken);

            foreach (var import in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var transportMessage = new FailedTransportMessage
                {
                    Id = import.MessageId,
                    Headers = MessageHeaders.Read(import.HeadersJson),
                    Body = import.Body
                };

                await onMessage(transportMessage, _ =>
                {
                    completed.Add(import.UniqueMessageId);
                    return Task.CompletedTask;
                }, cancellationToken);
            }

            if (batch.Count < BatchSize)
            {
                break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        await ExecuteWithDbContext(async (dbContext, token) =>
        {
            foreach (var chunk in completed.Chunk(1000))
            {
                await dbContext.FailedAuditImports
                    .Where(import => chunk.Contains(import.UniqueMessageId))
                    .ExecuteDeleteAsync(token);
            }
        }, cancellationToken);
    }

    public Task<int> GetFailedAuditsCount(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext((dbContext, token) => dbContext.FailedAuditImports.CountAsync(token), cancellationToken);

    static Guid Key(string? id) =>
        id is null ? Guid.NewGuid()
        : Guid.TryParse(id, out var parsed) ? parsed
        : DeterministicGuid.MakeId(id);
}
