namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class MessagesSubmittedForRetryFailed : IEvent
    {
        public string FailedMessageId { get; set; }
        public string Destination { get; set; }
        public string Reason { get; set; }
    }
}