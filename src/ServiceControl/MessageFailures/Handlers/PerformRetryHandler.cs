namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

            var attempt = failedMessage.ProcessingAttempts.Last();

            var headers = attempt.Message.Headers.Where(kv => !KeysToRemoveWhenRetryingAMessage.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);


            var transportMessage = new TransportMessage(failedMessage.MessageId,headers)
            {
                Body = attempt.Message.Body,
                CorrelationId = attempt.Message.CorrelationId,
                Recoverable = attempt.Message.Recoverable,
                MessageIntent = attempt.Message.MessageIntent,
                ReplyToAddress = Address.Parse(attempt.Message.ReplyToAddress)
            };

            Forwarder.Send(transportMessage, message.TargetEndpointAddress);
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