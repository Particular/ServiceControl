namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using InternalMessages;
    using NServiceBus;
    using NServiceBus.Transports;
    using Operations.BodyStorage;
    using Raven.Client;

    public class PerformRetryHandler : IHandleMessages<PerformRetry>
    {
        public IDocumentSession Session { get; set; }


        public ISendMessages Forwarder { get; set; }

        public IBodyStorage BodyStorage { get; set; }

        public void Handle(PerformRetry message)
        {
            var failedMessage = Session.Load<FailedMessage>(new Guid(message.FailedMessageId));

            if (failedMessage == null)
            {
                throw new ArgumentException("Can't find the failed message with id: " + message.FailedMessageId);
            }

            var attempt = failedMessage.MostRecentAttempt;

            var originalHeaders = attempt.Headers;

            var headersToRetryWith = originalHeaders.Where(kv => !KeysToRemoveWhenRetryingAMessage.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            headersToRetryWith["ServiceControl.RetryId"] = message.RetryId.ToString();


            using (var stream = BodyStorage.Fetch(message.FailedMessageId))
            {
                var transportMessage = new TransportMessage(failedMessage.Id, headersToRetryWith)
                {
                    Body = ReadFully(stream),
                    CorrelationId = attempt.CorrelationId,
                    Recoverable = attempt.Recoverable,
                    MessageIntent = attempt.MessageIntent,
                    ReplyToAddress = Address.Parse(attempt.ReplyToAddress)
                };

                failedMessage.Status = FailedMessageStatus.RetryIssued;

                Forwarder.Send(transportMessage, message.TargetEndpointAddress);
            }
        }


        static byte[] ReadFully(Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
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