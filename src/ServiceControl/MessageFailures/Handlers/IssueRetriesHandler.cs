namespace ServiceBus.Management.MessageFailures.Handlers
{
    using InternalMessages;
    using NServiceBus;

    public class IssueRetriesHandler : IHandleMessages<IssueRetries>
    {
        public IBus Bus { get; set; }

        public void Handle(IssueRetries message)
        {
            foreach (var messageId in message.MessageIds)
            {
                var messageToSend = new IssueRetry { MessageId = messageId };
                messageToSend.SetHeader("RequestedAt", Bus.CurrentMessageContext.Headers["RequestedAt"]);
                Bus.SendLocal(messageToSend);
            }
        }
    }
}