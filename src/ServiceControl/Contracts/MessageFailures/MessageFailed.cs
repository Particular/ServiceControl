namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;
    using Operations;

    public class MessageFailed : IEvent
    {
        public string EndpointId{ get; set; }
        public FailureDetails FailureDetails { get; set; }
        public string FailedMessageId { get; set; }
        public bool RepeatedFailure { get; set; }
    }
}
