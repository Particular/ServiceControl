namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using System.Text.Json;
using NServiceBus;
using NServiceBus.Transport;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.EntityConfigurations;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.UnitOfWork;

public class EFRecoverabilityIngestionUnitOfWork(EFIngestionUnitOfWork parentUnitOfWork, IBodyStoragePersistence storagePersistence, EFPersisterSettings settings) : IRecoverabilityIngestionUnitOfWork
{
    public Task RecordFailedProcessingAttempt(MessageContext context,
        FailedMessage.ProcessingAttempt processingAttempt,
        List<FailedMessage.FailureGroup> groups,
        CancellationToken cancellationToken = default)
    {
        var uniqueMessageId = context.Headers.UniqueId();
        var contentType = context.Headers.GetValueOrDefault(Headers.ContentType, "text/plain");
        var bodySize = context.Body.Length;
        var (bodyText, storeExternally) = MessageBodyClassifier.Classify(context.Headers, context.Body, settings.BodyStorage.MaxBodySizeToStore);

        if (storeExternally)
        {
            parentUnitOfWork.RecordBodyWrite(
                storagePersistence.WriteBody(uniqueMessageId, context.Body, contentType, cancellationToken));
        }

        var sendingEndpoint = GetMetadata<EndpointDetails>(processingAttempt, "SendingEndpoint");
        var receivingEndpoint = GetMetadata<EndpointDetails>(processingAttempt, "ReceivingEndpoint");

        parentUnitOfWork.Record(new RecordedFailedProcessingAttempt
        {
            UniqueMessageId = Guid.Parse(uniqueMessageId),
            AttemptedAt = processingAttempt.AttemptedAt,
            TimeOfFailure = processingAttempt.FailureDetails.TimeOfFailure,
            Groups = groups,
            HeadersJson = MessageHeaders.Write(processingAttempt.Headers),
            MessageId = processingAttempt.MessageId,
            MessageType = TruncateForColumn(GetMetadata<string>(processingAttempt, "MessageType")),
            TimeSent = GetMetadata<DateTime?>(processingAttempt, "TimeSent"),
            ConversationId = GetMetadata<string>(processingAttempt, "ConversationId"),
            SendingEndpointName = sendingEndpoint?.Name,
            SendingEndpointHostId = sendingEndpoint?.HostId,
            SendingEndpointHost = sendingEndpoint?.Host,
            ReceivingEndpointName = receivingEndpoint?.Name,
            ReceivingEndpointHostId = receivingEndpoint?.HostId,
            ReceivingEndpointHost = receivingEndpoint?.Host,
            ExceptionType = processingAttempt.FailureDetails.Exception?.ExceptionType,
            ExceptionMessage = processingAttempt.FailureDetails.Exception?.Message,
            FailingEndpointAddress = processingAttempt.FailureDetails.AddressOfFailingEndpoint,
            IsSystemMessage = GetMetadata<bool>(processingAttempt, "IsSystemMessage"),
            BodyText = bodyText,
            BodyStoredExternally = storeExternally,
            BodySize = bodySize,
            BodyContentType = contentType
        });

        return Task.CompletedTask;
    }

    public Task RecordSuccessfulRetry(string retriedMessageUniqueId, DateTime succeededAt, CancellationToken cancellationToken = default)
    {
        parentUnitOfWork.RecordConfirmedRetry(new ConfirmedRetry(Guid.Parse(retriedMessageUniqueId), succeededAt));

        return Task.CompletedTask;
    }

    // The MessageType column is length-bounded (ColumnLengths.ShortTextLength) so that it can be
    // an index key serving sort=message_type. The cap is enforced here, like the body size limit
    // above, because it is an EFCore storage limit the persister owns rather than a shared
    // ingestion rule. EnclosedMessageTypes' first comma token is a type's full name, so the cap can
    // only ever bite on pathological generic names, where a truncated sort key still sorts and
    // groups consistently.
    static string? TruncateForColumn(string? value) =>
        value is { Length: > ColumnLengths.ShortTextLength } ? value[..ColumnLengths.ShortTextLength] : value;

    static T? GetMetadata<T>(FailedMessage.ProcessingAttempt processingAttempt, string key) =>
        processingAttempt.MessageMetadata.TryGetValue(key, out var value) && value is T typed ? typed : default;
}
