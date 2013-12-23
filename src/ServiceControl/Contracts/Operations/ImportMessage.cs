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
            Metadata.Add("HeadersForSearching", new MessageMetadata("HeadersForSearching", 
                message.Headers.Select(kvp => kvp.Value).ToArray()));
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
}