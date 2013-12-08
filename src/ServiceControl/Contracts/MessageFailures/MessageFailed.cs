namespace ServiceControl.Contracts.MessageFailures
{
    using MessageAuditing;
    using NServiceBus;
    using Operations;

    public class MessageFailed : IEvent
    {
        public FailureDetails FailureDetails { get; set; }
        public string FailedMessageId { get; set; }
    }


    public class MessageFailedRepetedly : MessageFailed
    {
    }
}
