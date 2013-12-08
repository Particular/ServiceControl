namespace ServiceControl.Contracts.Operations
{
    using NServiceBus;

    public class FailedMessageDetected : IMessage
    {
        public string FailedMessageId { get; set; }

        public FailureDetails FailureDetails { get; set; }

        public PhysicalMessage PhysicalMessage { get; set; }
    }
}
