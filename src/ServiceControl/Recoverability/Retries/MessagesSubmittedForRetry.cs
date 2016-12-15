namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class MessagesSubmittedForRetry : IEvent
    {
        public string Context { get; set; }
        public string[] FailedMessageIds { get; set; }
        public int NumberOfFailedMessages { get; set; }

    }
}