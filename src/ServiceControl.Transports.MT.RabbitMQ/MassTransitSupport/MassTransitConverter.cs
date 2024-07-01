using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using MassTransit;
using MassTransit.Metadata;
using MassTransit.Serialization;
using NServiceBus.Faults;
using NsbHeaders = NServiceBus.Headers;
using MessageContext = NServiceBus.Transport.MessageContext;

class MassTransitConverter
{
    public static void To(MessageContext messageContext)
    {
        var headers = messageContext.Headers;

        foreach (var key in headers.Keys)
        {
            if (key.StartsWith("NServiceBus."))
            {
                headers.Remove(key);
            }
        }
    }

    public static void From(MessageContext messageContext)
    {
        var headers = messageContext.Headers;

        // TODO: Null/empty checks

        var messageEnvelope = DeserializeEnvelope(messageContext);

        // Could check and validate if envelope is returned, if not, try getting values from headers instead

        // MessageEnvelope
        headers[NsbHeaders.MessageId] = messageEnvelope.MessageId;
        headers[NsbHeaders.EnclosedMessageTypes] = string.Join(",", messageEnvelope.MessageType);
        headers[NsbHeaders.TimeSent] = DateTimeOffsetHelper.ToWireFormattedString(messageEnvelope.SentTime.Value);
        headers[NsbHeaders.ConversationId] = messageEnvelope.ConversationId;
        if (messageEnvelope.CorrelationId != null)
        {
            headers[NsbHeaders.CorrelationId] = messageEnvelope.CorrelationId;
        }

        if (messageEnvelope.ExpirationTime.HasValue)
        {
            var expirationTime = messageEnvelope.ExpirationTime.Value;
            headers[NsbHeaders.TimeToBeReceived] = DateTimeOffsetHelper.ToWireFormattedString(expirationTime);
        }

        headers[NsbHeaders.OriginatingEndpoint] = messageEnvelope.SourceAddress;

        // MT-Fault-***
        headers[NsbHeaders.DelayedRetries] = headers[MassTransit.MessageHeaders.FaultRetryCount];
        headers[NsbHeaders.ProcessingEndpoint] = headers[MassTransit.MessageHeaders.FaultInputAddress];
        headers[FaultsHeaderKeys.FailedQ] = headers[MassTransit.MessageHeaders.FaultInputAddress];
        headers[FaultsHeaderKeys.ExceptionType] = headers[MassTransit.MessageHeaders.FaultExceptionType];
        headers[FaultsHeaderKeys.Message] = headers[MassTransit.MessageHeaders.FaultMessage];
        headers[FaultsHeaderKeys.StackTrace] = headers[MassTransit.MessageHeaders.FaultStackTrace];
        headers[FaultsHeaderKeys.TimeOfFailure] = headers[MassTransit.MessageHeaders.FaultTimestamp];
    }

    static MessageEnvelope DeserializeEnvelope(MessageContext messageContext)
    {
        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        void Item(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Type == typeof(HostInfo))
            {
                typeInfo.CreateObject = () => new BusHostInfo();
            }
        }

        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { Item } };

        return JsonSerializer.Deserialize<JsonMessageEnvelope>(messageContext.Body.Span, options)
               ?? throw new InvalidOperationException();
    }
}