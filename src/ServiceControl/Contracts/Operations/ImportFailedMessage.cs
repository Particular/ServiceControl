namespace ServiceControl.Contracts.Operations
{
    using NServiceBus;

    public class ImportMessage : IMessage
    {
        public string UniqueMessageId { get; set; }

        public PhysicalMessage PhysicalMessage { get; set; }

        public EndpointDetails SendingEndpoint { get; set; }

        public EndpointDetails ReceivingEndpoint { get; set; }

    }


    public class ImportFailedMessage:ImportMessage
    {
        
        public FailureDetails FailureDetails { get; set; }

    }
}
