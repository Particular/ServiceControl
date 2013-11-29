namespace ServiceBus.Management.MessageFailures.Handlers
{
    using Infrastructure.RavenDB;
    using InternalMessages;
    using MessageAuditing;
    using NServiceBus;
    using NServiceBus.Transports;

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