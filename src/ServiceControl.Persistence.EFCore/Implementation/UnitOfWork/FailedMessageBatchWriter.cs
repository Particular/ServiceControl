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
class FailedMessageBatchWriter(ServiceControlDbContext dbContext, IFailedMessageIngestionSqlDialect dialect)
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

    // A message's groups are whatever its newest attempt classified it as, so they are replaced
    // rather than merged. Only the messages this batch is now the newest attempt for take part: an
    // older attempt arriving late from a concurrent writer already lost the payload columns in the
    // upsert, and leaving it the groups would describe one failure in the row and another in the
    // group rows. The upsert holds a row lock on every message in the batch until the transaction
    // commits, so no competing writer can act on these messages between the delete and the insert.
    async Task ReplaceGroups(List<FailedMessageEntity> failedMessages, List<FailedMessageGroupEntity> groups, CancellationToken cancellationToken)
    {
        var newestAttemptFor = await FindMessagesThisBatchIsNewestFor(failedMessages, cancellationToken);

        if (newestAttemptFor.Count == 0)
        {
            return;
        }

        await dbContext.FailedMessageGroups
            .Where(group => newestAttemptFor.Contains(group.FailedMessageUniqueId))
            .ExecuteDeleteAsync(cancellationToken);

        var replacements = groups.Where(group => newestAttemptFor.Contains(group.FailedMessageUniqueId)).ToList();

        if (replacements.Count > 0)
        {
            await dialect.InsertGroups(dbContext, replacements, cancellationToken);
        }
    }

    // The upsert has just stored the later of the incoming and the already stored attempt, so this
    // batch is the newest attempt for exactly those messages whose stored value it now matches.
    //
    // Reading the value back rather than comparing against what the batch sent is what keeps this
    // in step with the upsert's own guard on a provider whose column precision is coarser than
    // DateTime: it truncates the stored value, which is why the comparison is <= and not ==.
    async Task<HashSet<Guid>> FindMessagesThisBatchIsNewestFor(List<FailedMessageEntity> failedMessages, CancellationToken cancellationToken)
    {
        var messageIds = failedMessages.Select(message => message.UniqueMessageId).ToArray();

        var storedAttempts = await dbContext.FailedMessages
            .AsNoTracking()
            .Where(message => messageIds.Contains(message.UniqueMessageId))
            .Select(message => new { message.UniqueMessageId, message.LastAttemptedAt })
            .ToDictionaryAsync(row => row.UniqueMessageId, row => row.LastAttemptedAt, cancellationToken);

        return
        [
            .. failedMessages
                .Where(message => storedAttempts.TryGetValue(message.UniqueMessageId, out var storedAttempt)
                                  && storedAttempt <= message.LastAttemptedAt)
                .Select(message => message.UniqueMessageId)
        ];
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
