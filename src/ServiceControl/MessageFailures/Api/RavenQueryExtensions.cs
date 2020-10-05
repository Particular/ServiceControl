namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;

    static class RavenQueryExtensions
    {
        public static IEnumerable<FailedMessageView> ToFailedMessageView(this IEnumerable<FailedMessage> query)
            => from message in query
                let attempt = message.ProcessingAttempts.OrderByDescending(a => a.AttemptedAt).FirstOrDefault()
                let edited = attempt.Headers.TryGetValue("ServiceControl.EditOf", out _)
                let timeSent = attempt.Meta<string>("TimeSent")
                select new FailedMessageView
                {
                    Id = message.UniqueMessageId,
                    MessageType = attempt.Meta<string>("MessageType"),
                    IsSystemMessage = attempt.Meta<bool>("IsSystemMessage"),
                    SendingEndpoint = attempt.Meta<EndpointDetails>("SendingEndpoint"),
                    ReceivingEndpoint = attempt.Meta<EndpointDetails>("ReceivingEndpoint"),
                    TimeSent = timeSent == null ? default(DateTime?) : DateTime.SpecifyKind(DateTime.Parse(timeSent), DateTimeKind.Utc),
                    MessageId = attempt.MessageId,
                    Exception = attempt.FailureDetails.Exception,
                    QueueAddress = attempt.FailureDetails.AddressOfFailingEndpoint,
                    NumberOfProcessingAttempts = message.ProcessingAttempts.Count(),
                    Status = message.Status,
                    TimeOfFailure = attempt.FailureDetails.TimeOfFailure,
                    //LastModified = ??
                    Edited = edited,
                    EditOf = edited ? attempt.Headers["ServiceControl.EditOf"] : ""
                };

        static T Meta<T>(this FailedMessage.ProcessingAttempt attempt, string key, T defaultValue = default)
        {
            if (attempt.MessageMetadata.TryGetValue(key, out var value) && value != null)
            {
                if (value is T convertedValue)
                {
                    return convertedValue;
                }

                throw new Exception($"{key} is not {typeof(T).Name}");
            }

            return defaultValue;
        }
    }
}