namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using InternalMessages;
    using NServiceBus;
    using NServiceBus.Transports;
    using ServiceBus.Management.Infrastructure.RavenDB;

    public class PerformRetryHandler : IHandleMessages<PerformRetry>
    {
        public RavenUnitOfWork RavenUnitOfWork { get; set; }

        public ISendMessages Forwarder { get; set; }

        public void Handle(PerformRetry message)
        {
            var failedMessage = RavenUnitOfWork.Session.Load<FailedMessage>(message.FailedMessageId);

            if (failedMessage == null)
            {
                throw new ArgumentException("Can't find e failed message with id: " + message.FailedMessageId);
            }

            var attempt = failedMessage.MostRecentAttempt;

            var originalHeaders = attempt.Headers;

            var headersToRetryWith = originalHeaders.Where(kv => !KeysToRemoveWhenRetryingAMessage.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            headersToRetryWith["ServiceControl.RetryId"] = message.RetryId.ToString();

            var bodyUrl = attempt.MessageMetadata["BodyUrl"].Value;

            using (var client = new WebClient())
            {
                var transportMessage = new TransportMessage(failedMessage.Id, headersToRetryWith)
                {
                    Body = client.DownloadData(bodyUrl.ToString()),
                    CorrelationId = attempt.CorrelationId,
                    Recoverable = attempt.Recoverable,
                    MessageIntent = attempt.MessageIntent,
                    ReplyToAddress = Address.Parse(attempt.ReplyToAddress)
                };
                
                failedMessage.Status = FailedMessageStatus.RetryIssued;

                Forwarder.Send(transportMessage, message.TargetEndpointAddress);
            }
        }

        static readonly List<string> KeysToRemoveWhenRetryingAMessage = new List<string>
        {
            Headers.Retries,
            "NServiceBus.FailedQ",
            "NServiceBus.TimeOfFailure",
            "NServiceBus.ExceptionInfo.ExceptionType",
            "NServiceBus.ExceptionInfo.AuditMessage",
            "NServiceBus.ExceptionInfo.Source",
            "NServiceBus.ExceptionInfo.StackTrace"
        };
    }
}