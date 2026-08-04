namespace ServiceControl.Persistence.EFCore.Implementation;

using System.Text.Json;
using ServiceControl.Contracts.Operations;
using ServiceControl.MessageFailures;
using ServiceControl.MessageFailures.Api;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

static class FailedMessageViewMapper
{
    const string EditOfHeader = "ServiceControl.EditOf";
    const string ExceptionSourceHeader = "NServiceBus.ExceptionInfo.Source";
    const string ExceptionStackTraceHeader = "NServiceBus.ExceptionInfo.StackTrace";

    public static FailedMessageView ToFailedMessageView(this FailedMessageEntity entity)
    {
        var headers = entity.ReadHeaders();
        var editOf = headers.GetValueOrDefault(EditOfHeader);

        return new FailedMessageView
        {
            Id = entity.UniqueMessageId.ToString(),
            MessageType = entity.MessageType,
            IsSystemMessage = entity.IsSystemMessage,
            TimeSent = entity.TimeSent,
            MessageId = entity.MessageId,
            Exception = entity.ToExceptionDetails(headers),
            QueueAddress = entity.FailingEndpointAddress,
            NumberOfProcessingAttempts = entity.NumberOfProcessingAttempts,
            Status = entity.Status,
            SendingEndpoint = entity.ToSendingEndpoint(),
            ReceivingEndpoint = entity.ToReceivingEndpoint(),
            TimeOfFailure = entity.LastTimeOfFailure,
            LastModified = entity.LastModified,
            Edited = editOf != null,
            EditOf = editOf ?? string.Empty
        };
    }

    public static FailedMessage ToFailedMessage(this FailedMessageEntity entity, IReadOnlyCollection<FailedMessageGroupEntity> groups)
    {
        var headers = entity.ReadHeaders();

        var attempt = new FailedMessage.ProcessingAttempt
        {
            AttemptedAt = entity.LastAttemptedAt,
            MessageId = entity.MessageId,
            Headers = headers,
            Body = entity.BodyText,
            MessageMetadata = entity.ToMetadata(),
            FailureDetails = new FailureDetails
            {
                TimeOfFailure = entity.LastTimeOfFailure,
                AddressOfFailingEndpoint = entity.FailingEndpointAddress,
                Exception = entity.ToExceptionDetails(headers)
            }
        };

        return new FailedMessage
        {
            Id = entity.UniqueMessageId.ToString(),
            UniqueMessageId = entity.UniqueMessageId.ToString(),
            Status = entity.Status,
            ProcessingAttempts = BuildAttempts(attempt, entity.NumberOfProcessingAttempts),
            FailureGroups = [.. groups.Select(group => new FailedMessage.FailureGroup
            {
                Id = group.GroupId,
                Title = group.Title,
                Type = group.Type
            })]
        };
    }

    static List<FailedMessage.ProcessingAttempt> BuildAttempts(FailedMessage.ProcessingAttempt last, int numberOfAttempts)
    {
        var attempts = new List<FailedMessage.ProcessingAttempt>(Math.Max(numberOfAttempts, 1));

        for (var i = 1; i < numberOfAttempts; i++)
        {
            attempts.Add(new FailedMessage.ProcessingAttempt());
        }

        attempts.Add(last);

        return attempts;
    }

    static Dictionary<string, object> ToMetadata(this FailedMessageEntity entity)
    {
        var metadata = new Dictionary<string, object>
        {
            ["IsSystemMessage"] = entity.IsSystemMessage,
            ["ContentLength"] = entity.BodySize
        };

        AddIfPresent(metadata, "MessageId", entity.MessageId);
        AddIfPresent(metadata, "MessageType", entity.MessageType);
        AddIfPresent(metadata, "ConversationId", entity.ConversationId);
        AddIfPresent(metadata, "ContentType", entity.BodyContentType);
        AddIfPresent(metadata, "SendingEndpoint", entity.ToSendingEndpoint());
        AddIfPresent(metadata, "ReceivingEndpoint", entity.ToReceivingEndpoint());

        if (entity.TimeSent.HasValue)
        {
            metadata["TimeSent"] = entity.TimeSent.Value;
        }

        return metadata;
    }

    static void AddIfPresent(Dictionary<string, object> metadata, string key, object? value)
    {
        if (value != null)
        {
            metadata[key] = value;
        }
    }

    static ExceptionDetails ToExceptionDetails(this FailedMessageEntity entity, Dictionary<string, string> headers) =>
        new()
        {
            ExceptionType = entity.ExceptionType,
            Message = entity.ExceptionMessage,
            Source = headers.GetValueOrDefault(ExceptionSourceHeader),
            StackTrace = headers.GetValueOrDefault(ExceptionStackTraceHeader)
        };

    static EndpointDetails? ToSendingEndpoint(this FailedMessageEntity entity) =>
        entity.SendingEndpointName == null
            ? null
            : new EndpointDetails
            {
                Name = entity.SendingEndpointName,
                Host = entity.SendingEndpointHost,
                HostId = entity.SendingEndpointHostId ?? Guid.Empty
            };

    static EndpointDetails? ToReceivingEndpoint(this FailedMessageEntity entity) =>
        entity.ReceivingEndpointName == null
            ? null
            : new EndpointDetails
            {
                Name = entity.ReceivingEndpointName,
                Host = entity.ReceivingEndpointHost,
                HostId = entity.ReceivingEndpointHostId ?? Guid.Empty
            };

    static Dictionary<string, string> ReadHeaders(this FailedMessageEntity entity) => MessageHeaders.Read(entity.HeadersJson);
}
