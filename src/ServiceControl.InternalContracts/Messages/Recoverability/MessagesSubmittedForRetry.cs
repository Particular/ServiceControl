namespace ServiceControl.InternalContracts.Messages.Recoverability
{
    using NServiceBus;

    public class MessagesSubmittedForRetry : IEvent
    {
        public string[] FailedMessageIds { get; set; }
    }
}
