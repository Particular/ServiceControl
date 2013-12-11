namespace ServiceControl.MessageFailures.Handlers
{
    using System;
    using Contracts.MessageFailures;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.RavenDB;

    public class MessageFailureResolvedHandler : IHandleMessages<MessageFailureResolved>
    {
        public RavenUnitOfWork RavenUnitOfWork { get; set; }

        public void Handle(MessageFailureResolved message)
        {
            var failedMessage = RavenUnitOfWork.Session.Load<FailedMessage>(message.FailedMessageId);

            if (failedMessage == null)
            {
                throw new ArgumentException("Can't find e failed message with id: " + message.FailedMessageId);
            }

            if (message.GetType() == typeof(MessageFailureResolvedByArchiving))
            {
                failedMessage.Status = FailedMessageStatus.Archived;
            }
            else
            {
                failedMessage.Status = FailedMessageStatus.Resolved;    
            }
            
        }
    }
}