namespace ServiceControl.MessageFailures.Handlers
{
    using InternalMessages;
    using NServiceBus;
    using NServiceBus.Transports;
    using ServiceBus.Management.Infrastructure.RavenDB;
    using ServiceBus.Management.MessageAuditing;

    public class IssueRetryHandler : IHandleMessages<IssueRetry>
    {
        public RavenUnitOfWork RavenUnitOfWork { get; set; }

        public ISendMessages Forwarder { get; set; }

        public void Handle(IssueRetry message)
        {
            var failedMessage = RavenUnitOfWork.Session.Load<Message>(message.MessageId);

            if (failedMessage == null)
            {
                return;
            }

            var requestedAtHeader = message.GetHeader("RequestedAt");
            var transportMessage = failedMessage.IssueRetry(DateTimeExtensions.ToUtcDateTime(requestedAtHeader));

            Forwarder.Send(transportMessage, Address.Parse(failedMessage.FailureDetails.FailedInQueue));
        }
    }
}