namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

// Writes one ingestion batch inside a single transaction. The statements providers genuinely
// differ on (the upserts) come from the injected dialect; everything portable stays here as
// set-based EF operations. Statement order matters: a message that fails and is retry-confirmed
// in the same batch must end Resolved.
class FailedMessageBatchWriter(ServiceControlDbContext dbContext, IIngestionSqlDialect dialect)
{
    public async Task Write(
        IReadOnlyCollection<RecordedFailedProcessingAttempt> attempts,
        IReadOnlyCollection<KnownEndpoint> knownEndpoints,
        IReadOnlyCollection<Guid> confirmedRetries,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var (failedMessages, groups) = Fold(attempts, now);
        var endpoints = BuildEndpointRows(knownEndpoints);
        var retries = confirmedRetries.Distinct().ToArray();

        if (failedMessages.Count == 0 && endpoints.Count == 0 && retries.Length == 0)
        {
            return;
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

            if (failedMessages.Count > 0)
            {
                await dialect.UpsertFailedMessages(dbContext, failedMessages, ct);
                await ReplaceGroups(failedMessages, groups, ct);
            }

            if (endpoints.Count > 0)
            {
                await dialect.InsertMissingKnownEndpoints(dbContext, endpoints, ct);
            }

            if (retries.Length > 0)
            {
                await ResolveRetried(retries, now, ct);
            }

            await transaction.CommitAsync(ct);
        }, cancellationToken);
    }

    static (List<FailedMessageEntity> Messages, List<FailedMessageGroupEntity> Groups) Fold(
        IReadOnlyCollection<RecordedFailedProcessingAttempt> attempts, DateTime now)
    {
        var messages = new List<FailedMessageEntity>();
        var groups = new List<FailedMessageGroupEntity>();

        foreach (var group in attempts.GroupBy(attempt => attempt.UniqueMessageId).OrderBy(group => group.Key))
        {
            var ordered = group.OrderBy(attempt => attempt.AttemptedAt).ToList();
            var last = ordered[^1];

            messages.Add(new FailedMessageEntity
            {
                UniqueMessageId = group.Key,
                Status = FailedMessageStatus.Unresolved,
                StatusChangedAt = now,
                LastModified = now,
                NumberOfProcessingAttempts = ordered.Select(attempt => attempt.AttemptedAt).Distinct().Count(),
                FirstTimeOfFailure = ordered.Min(attempt => attempt.TimeOfFailure),
                LastTimeOfFailure = ordered.Max(attempt => attempt.TimeOfFailure),
                LastAttemptedAt = last.AttemptedAt,
                MessageId = last.MessageId,
                MessageType = last.MessageType,
                TimeSent = last.TimeSent,
                ConversationId = last.ConversationId,
                QueueAddress = last.QueueAddress,
                SendingEndpointName = last.SendingEndpointName,
                SendingEndpointHostId = last.SendingEndpointHostId,
                SendingEndpointHost = last.SendingEndpointHost,
                ReceivingEndpointName = last.ReceivingEndpointName,
                ReceivingEndpointHostId = last.ReceivingEndpointHostId,
                ReceivingEndpointHost = last.ReceivingEndpointHost,
                ExceptionType = last.ExceptionType,
                ExceptionMessage = last.ExceptionMessage,
                IsSystemMessage = last.IsSystemMessage,
                HeadersJson = last.HeadersJson,
                BodyText = last.BodyText,
                BodyStoredExternally = last.BodyStoredExternally,
                BodySize = last.BodySize,
                BodyContentType = last.BodyContentType,
                FailingEndpointAddress = last.FailingEndpointAddress
            });

            groups.AddRange(last.Groups
                .Where(failureGroup => failureGroup.Id != null)
                .DistinctBy(failureGroup => failureGroup.Id)
                .Select(failureGroup => new FailedMessageGroupEntity
                {
                    FailedMessageUniqueId = group.Key,
                    GroupId = failureGroup.Id,
                    Title = failureGroup.Title ?? string.Empty,
                    Type = failureGroup.Type ?? string.Empty
                }));
        }

        return (messages, groups);
    }

    static List<KnownEndpointEntity> BuildEndpointRows(IReadOnlyCollection<KnownEndpoint> knownEndpoints) =>
        [.. knownEndpoints
            .Select(knownEndpoint => new KnownEndpointEntity
            {
                Id = knownEndpoint.EndpointDetails.GetDeterministicId(),
                Name = knownEndpoint.EndpointDetails.Name,
                HostId = knownEndpoint.EndpointDetails.HostId,
                Host = knownEndpoint.EndpointDetails.Host,
                Monitored = false
            })
            .DistinctBy(endpoint => endpoint.Id)];

    // Group rows are replaced wholesale on every attempt. When two concurrent writers process the
    // same message their delete/insert pairs can interleave into a transient union of both
    // attempts' groups; that is accepted, the next attempt replaces it.
    async Task ReplaceGroups(List<FailedMessageEntity> failedMessages, List<FailedMessageGroupEntity> groups, CancellationToken cancellationToken)
    {
        var messageIds = failedMessages.Select(message => message.UniqueMessageId).ToArray();

        await dbContext.FailedMessageGroups
            .Where(group => messageIds.Contains(group.FailedMessageUniqueId))
            .ExecuteDeleteAsync(cancellationToken);

        if (groups.Count > 0)
        {
            await dialect.InsertGroups(dbContext, groups, cancellationToken);
        }
    }

    async Task ResolveRetried(Guid[] retries, DateTime now, CancellationToken cancellationToken)
    {
        await dbContext.FailedMessages
            .Where(failedMessage => retries.Contains(failedMessage.UniqueMessageId))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(failedMessage => failedMessage.Status, FailedMessageStatus.Resolved)
                .SetProperty(failedMessage => failedMessage.StatusChangedAt, now)
                .SetProperty(failedMessage => failedMessage.LastModified, now), cancellationToken);

        await dbContext.FailedMessageRetries
            .Where(retry => retries.Contains(retry.UniqueMessageId))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
