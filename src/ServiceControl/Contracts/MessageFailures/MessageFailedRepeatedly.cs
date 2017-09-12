namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;
    using ServiceControl.Contracts.Operations;

    // NOTE: This is a legacy event. It (and all it's handlers) have been left to capture in-flight messages. These can removed in a future version.
    public class MessageFailedRepeatedly : IEvent
    {
        public MessageFailedRepeatedly()
        {
            RepeatedFailure = true;
        }

        public string EndpointId { get; set; }
        public FailureDetails FailureDetails { get; set; }
        public string FailedMessageId { get; set; }
        public bool RepeatedFailure { get; set; }

    }
}