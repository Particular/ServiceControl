namespace ServiceControl.Contracts.MessageFailures
{
    using NServiceBus;
    using Operations;

    public class MessageFailed : IEvent
    {
        public EndpointDetails Endpoint { get; set; }
        public FailureDetails FailureDetails { get; set; }
        public string FailedMessageId { get; set; }
    }


    public class MessageFailedRepetedly : MessageFailed
    {
    }
}
