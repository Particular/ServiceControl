namespace ServiceControl.Contracts.Operations
{
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;

    public class ImportMessage : IMessage
    {
        protected ImportMessage(TransportMessage message)
        {
            UniqueMessageId = message.UniqueId();
            MessageId = message.Id;
            
            PhysicalMessage = new PhysicalMessage(message);
        
            Metadata = new Dictionary<string, MessageMetadata>();

            //add basic message metadata
            Metadata.Add("MessageId",new MessageMetadata("MessageId",message.Id));
            Metadata.Add("MessageIntent", new MessageMetadata("MessageIntent",message.MessageIntent));
            Metadata.Add("HeadersForSearching", new MessageMetadata("HeadersForSearching", "Count: " + message.Headers.Count, 
                message.Headers.Select(kvp => string.Format("{0} {1}", kvp.Key, kvp.Value)).ToArray()));
        }

        public string UniqueMessageId { get; set; }
        public string MessageId { get; set; }

        public PhysicalMessage PhysicalMessage { get; set; }

        public Dictionary<string, MessageMetadata> Metadata { get; set; }

        public void Add(MessageMetadata metadata)
        {
            Metadata[metadata.Name] = metadata;
        }
    }


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
