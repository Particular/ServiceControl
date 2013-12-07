namespace ServiceControl.Contracts.Operations
{
    using System.Collections.Generic;
    using MessageAuditing;
    using NServiceBus;

    public class FailedMessageDetected : IMessage
    {
        public string FailedMessageId { get; set; }

        public FailureDetails FailureDetails { get; set; }

        public PhysicalMessage PhysicalMessage { get; set; }
    }


    public class PhysicalMessage
    {
        public PhysicalMessage()
        {
            
        }

        public PhysicalMessage(TransportMessage message)
        {
            MessageId = message.Id;
            Headers = message.Headers;
            Body = message.Body;
            ReplyToAddress = message.ReplyToAddress.ToString();
            ProcessingEndpoint = EndpointDetails.ReceivingEndpoint(Headers);
        }

        public EndpointDetails ProcessingEndpoint { get; set; }

        public string MessageId { get; set; }
        public byte[] Body { get; set; }

        public IDictionary<string, string> Headers { get; set; }
        public string ReplyToAddress { get; set; }

    }



}
