namespace ServiceBus.Management.Errors
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
                throw new InvalidOperationException(string.Format("Retry failed, message {0} could not be found", message.MessageId));

            var transportMessage = failedMessage.IssueRetry(DateTime.Parse(message.GetHeader("RequestedAt")));

            
            Forwarder.Send(transportMessage, Address.Parse(failedMessage.FailureDetails.FailedInQueue));
        }
    }
}