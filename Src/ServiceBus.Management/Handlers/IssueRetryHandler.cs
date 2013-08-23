namespace ServiceBus.Management.Handlers
{
    using System;
    using Commands;
    using NServiceBus;
    using NServiceBus.Transports;
    using RavenDB;

    public class IssueRetryHandler : IHandleMessages<IssueRetry>
    {
        public RavenUnitOfWork RavenUnitOfWork { get; set; }

        public ISendMessages Forwarder { get; set; }

        public void Handle(IssueRetry message)
        {
            var failedMessage = RavenUnitOfWork.Session.Load<Message>(message.MessageId);

            if (failedMessage == null)
            {
                throw new InvalidOperationException(string.Format("Retry failed, message {0} could not be found",
                    message.MessageId));
            }

            var requestedAtHeader = message.GetHeader("RequestedAt");
            var transportMessage = failedMessage.IssueRetry(DateTimeExtensions.ToUtcDateTime(requestedAtHeader));

            Forwarder.Send(transportMessage, Address.Parse(failedMessage.FailureDetails.FailedInQueue));
        }
    }
}