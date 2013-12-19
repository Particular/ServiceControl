namespace ServiceControl.Contracts.Operations
{
    using NServiceBus;


    public class ImportFailedMessage : ImportMessage
    {
        public ImportFailedMessage(TransportMessage message)
            : base(message)
        {
            FailureDetails = new FailureDetails(message.Headers);
            FailingEndpointId = FailureDetails.AddressOfFailingEndpoint.Queue;
        }

        public string FailingEndpointId { get; set; }

        public FailureDetails FailureDetails { get; set; }

    }
}
