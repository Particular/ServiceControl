namespace ServiceControl.MessageFailures.Handlers
{
    using InternalMessages;
    using NServiceBus;

    public class IssueRetriesHandler : IHandleMessages<RequestRetries>
    {
        public IBus Bus { get; set; }

        public void Handle(RequestRetries message)
        {
            foreach (var messageId in message.MessageIds)
            {
                var messageToSend = new RequestRetry { FailedMessageId = messageId };
                messageToSend.SetHeader("RequestedAt", Bus.CurrentMessageContext.Headers["RequestedAt"]);
                Bus.SendLocal(messageToSend);
            }
        }
    }
}