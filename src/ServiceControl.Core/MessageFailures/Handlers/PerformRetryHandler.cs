namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Contracts.MessageFailures;
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

        public IBus Bus { get; set; }

        public void Handle(PerformRetry message)
        {
            var failedMessage = Session.Load<MessageFailureHistory>(new Guid(message.FailedMessageId));

            if (failedMessage == null)
            {
                throw new ArgumentException("Can't find the failed message with id: " + message.FailedMessageId);
            }

            if (failedMessage.Status != FailedMessageStatus.Unresolved)
            {
                // We only retry messages that are unresolved
                return;
            }

            var attempt = failedMessage.ProcessingAttempts.Last();

            var originalHeaders = attempt.Headers;

            var headersToRetryWith = originalHeaders.Where(kv => !KeysToRemoveWhenRetryingAMessage.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            headersToRetryWith["ServiceControl.RetryId"] = message.RetryId.ToString();

            using (var stream = BodyStorage.Fetch(attempt.MessageId))
            {
                var transportMessage = new TransportMessage(failedMessage.Id, headersToRetryWith)
                {
                    Body = ReadFully(stream),
                    CorrelationId = attempt.CorrelationId,
                    Recoverable = attempt.Recoverable,
                    MessageIntent = (MessageIntentEnum)Enum.Parse(typeof(MessageIntentEnum), attempt.MessageIntent, true),
                };

                if (!String.IsNullOrWhiteSpace(attempt.ReplyToAddress))
                {
                    transportMessage.ReplyToAddress = Address.Parse(attempt.ReplyToAddress);
                }

                failedMessage.Status = FailedMessageStatus.RetryIssued;

                Forwarder.Send(transportMessage, message.TargetEndpointAddress);
            }

            Bus.Publish<MessageSubmittedForRetry>(m => m.FailedMessageId = message.FailedMessageId);
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