namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using System.Text.Json;
using NServiceBus;
using NServiceBus.Transport;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.UnitOfWork;

public class EFRecoverabilityIngestionUnitOfWork(EFIngestionUnitOfWork parentUnitOfWork, IBodyStoragePersistence storagePersistence, EFPersisterSettings settings) : IRecoverabilityIngestionUnitOfWork
{
    public Task RecordFailedProcessingAttempt(MessageContext context,
        FailedMessage.ProcessingAttempt processingAttempt,
        List<FailedMessage.FailureGroup> groups)
    {
        var uniqueMessageId = context.Headers.UniqueId();
        var contentType = context.Headers.GetValueOrDefault(Headers.ContentType, "text/plain");
        var bodySize = context.Body.Length;
        var (bodyText, storeExternally) = MessageBodyClassifier.Classify(context.Headers, context.Body, settings.MaxBodySizeToStore);

        if (storeExternally)
        {
            parentUnitOfWork.RecordBodyWrite(
                storagePersistence.WriteBody(uniqueMessageId, context.Body, contentType));
        }

        var sendingEndpoint = GetMetadata<EndpointDetails>(processingAttempt, "SendingEndpoint");
        var receivingEndpoint = GetMetadata<EndpointDetails>(processingAttempt, "ReceivingEndpoint");

        parentUnitOfWork.Record(new RecordedFailedProcessingAttempt
        {
            UniqueMessageId = Guid.Parse(uniqueMessageId),
            AttemptedAt = processingAttempt.AttemptedAt,
            TimeOfFailure = processingAttempt.FailureDetails.TimeOfFailure,
            Groups = groups,
            HeadersJson = JsonSerializer.Serialize(processingAttempt.Headers, HeadersJsonContext.Default.DictionaryStringString),
            MessageId = processingAttempt.MessageId,
            MessageType = GetMetadata<string>(processingAttempt, "MessageType"),
            TimeSent = GetMetadata<DateTime?>(processingAttempt, "TimeSent"),
            ConversationId = GetMetadata<string>(processingAttempt, "ConversationId"),
            QueueAddress = context.Headers.GetValueOrDefault(NServiceBus.Faults.FaultsHeaderKeys.FailedQ),
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

    public Task RecordSuccessfulRetry(string retriedMessageUniqueId)
    {
        parentUnitOfWork.RecordConfirmedRetry(Guid.Parse(retriedMessageUniqueId));

        return Task.CompletedTask;
    }

    static T? GetMetadata<T>(FailedMessage.ProcessingAttempt processingAttempt, string key) =>
        processingAttempt.MessageMetadata.TryGetValue(key, out var value) && value is T typed ? typed : default;
}
