#nullable enable
namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EFCore.DbContexts;
using EFCore.Entities;
using EFCore.Implementation.UnitOfWork;
using MessageFailures;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using Operations;

public partial class PersistenceTestsContext
{
    static async Task InsertFailedMessagesDirect(IServiceProvider serviceProvider, FailedMessage[] messages)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await using var db = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        static T? GetMetadata<T>(FailedMessage.ProcessingAttempt processingAttempt, string key) =>
            processingAttempt.MessageMetadata.TryGetValue(key, out var value) && value is T typed ? typed : default;

        foreach (FailedMessage failedMessage in messages)
        {
            var now = DateTime.UtcNow;
            var ordered = failedMessage.ProcessingAttempts
                .OrderBy(x => x.AttemptedAt)
                .ThenBy(x => x.MessageId)
                .ToList();
            var attempt = ordered.Last();
            var sendingEndpoint = GetMetadata<EndpointDetails>(attempt, "SendingEndpoint");
            var receivingEndpoint = GetMetadata<EndpointDetails>(attempt, "ReceivingEndpoint");
            var contentType = attempt.Headers.GetValueOrDefault(Headers.ContentType, "text/plain");
            db.FailedMessages.Add(new FailedMessageEntity
            {
                UniqueMessageId = Guid.Parse(failedMessage.UniqueMessageId),
                FirstTimeOfFailure = ordered.Min(pa => pa.FailureDetails.TimeOfFailure),
                LastTimeOfFailure = ordered.Max(pa => pa.FailureDetails.TimeOfFailure),
                LastAttemptedAt = attempt.AttemptedAt,
                HeadersJson = JsonSerializer.Serialize(attempt.Headers, HeadersJsonContext.Default.DictionaryStringString),
                MessageId = attempt.MessageId,
                MessageType = GetMetadata<string>(attempt, "MessageType"),
                TimeSent = GetMetadata<DateTime?>(attempt, "TimeSent"),
                ConversationId = GetMetadata<string>(attempt, "ConversationId"),
                SendingEndpointName = sendingEndpoint?.Name,
                SendingEndpointHostId = sendingEndpoint?.HostId,
                SendingEndpointHost = sendingEndpoint?.Host,
                ReceivingEndpointName = receivingEndpoint?.Name,
                ReceivingEndpointHostId = receivingEndpoint?.HostId,
                ReceivingEndpointHost = receivingEndpoint?.Host,
                ExceptionType = attempt.FailureDetails.Exception?.ExceptionType,
                ExceptionMessage = attempt.FailureDetails.Exception?.Message,
                FailingEndpointAddress = attempt.FailureDetails.AddressOfFailingEndpoint,
                IsSystemMessage = GetMetadata<bool>(attempt, "IsSystemMessage"),
                BodyText = attempt.Body,
                BodyStoredExternally = false,
                BodySize = attempt.Body?.Length ?? 0,
                BodyContentType = contentType,
                Status = failedMessage.Status,
                StatusChangedAt = now,
                LastModified = now,
                NumberOfProcessingAttempts = ordered.Select(pa => pa.AttemptedAt).Distinct().Count(),
            });

            // RavenDB carries the failure groups inside the document, so seeding them has to be
            // explicit here for the two persisters to start from the same state.
            db.FailedMessageGroups.AddRange(failedMessage.FailureGroups
                .Where(group => group.Id != null)
                .DistinctBy(group => group.Id)
                .Select(group => new FailedMessageGroupEntity
                {
                    FailedMessageUniqueId = Guid.Parse(failedMessage.UniqueMessageId),
                    GroupId = group.Id,
                    Title = group.Title ?? string.Empty,
                    Type = group.Type ?? string.Empty
                }));
        }

        await db.SaveChangesAsync();
    }
}