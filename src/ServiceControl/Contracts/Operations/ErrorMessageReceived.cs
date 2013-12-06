namespace ServiceControl.Contracts.Operations
{
    using System.Collections.Generic;
    using NServiceBus;
    using ServiceBus.Management.MessageAuditing;

    public class ErrorMessageReceived : IMessage
    {
        public string ErrorMessageId { get; set; }

        public FailureDetails FailureDetails { get; set; }

        public Message2 PhysicalMessage { get; set; }
    }


    public class Message2
    {
        public Message2()
        {
            
        }

        public Message2(TransportMessage message)
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
