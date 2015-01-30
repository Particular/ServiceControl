namespace ServiceControl.MessageFailures.InternalMessages
{
    using NServiceBus;
    using ServiceControl.Contracts.Operations;

    public class ImportFailedMessage : ICommand
    {
        public string UniqueMessageId { get; set; }
        public string FailingEndpointName { get; set; }
        public FailureDetails FailureDetails { get; set; }
        public string MessageType { get; set; }
        public string RetryId { get; set; }
    }
}