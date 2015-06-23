namespace ServiceControl.MessageFailures
{
    using System;
    using NServiceBus;
    using Particular.Operations.Ingestion.Api;
    using ServiceControl.Contracts.Operations;

    public class ProcessingAttemptMessageFailureHistoryEnricher : IEnrichMessageFailureHistory
    {
        public void Enrich(MessageFailureHistory history, IngestedMessage actualMessage, FailureDetails failureDetails)
        {
            history.ProcessingAttempts.Add(new MessageFailureHistory.ProcessingAttempt
            {
                ProcessingEndpoint = new EndpointDetails()
                {
                    Name = actualMessage.ProcessedAt.EndpointName,
                    HostId = actualMessage.ProcessedAt.HostId
                },
                SendingEndpoint = new EndpointDetails()
                {
                    Name = actualMessage.SentFrom.EndpointName,
                    HostId = actualMessage.SentFrom.HostId
                },
                ContentType = actualMessage.Headers.GetOrDefault("NServiceBus.ContentType", "text/xml"),
                MessageType = actualMessage.MessageType.Name,
                IsSystemMessage = actualMessage.MessageType.IsSystem,
                TimeSent = ParseSentTime(actualMessage.Headers),
                AttemptedAt = failureDetails.TimeOfFailure,
                FailureDetails = failureDetails,
                MessageId = actualMessage.Id,
                Headers = actualMessage.Headers.ToDictionary(),
                ReplyToAddress = actualMessage.Headers.GetOrDefault("NServiceBus.ReplyToAddress"),
                Recoverable = actualMessage.Recoverable,
                CorrelationId = actualMessage.Headers.GetOrDefault("NServiceBus.CorrelationId"),
                MessageIntent = actualMessage.Headers.GetOrDefault("NServiceBus.MessageIntent"),

            });
        }

        static DateTime ParseSentTime(HeaderCollection headers)
        {
            string timeSentValue;
            if (headers.TryGet(Headers.TimeSent, out timeSentValue))
            {
                var timeSent = DateTimeExtensions.ToUtcDateTime(timeSentValue);
                return timeSent;
            }
            return DateTime.MinValue;
        }
    }
}