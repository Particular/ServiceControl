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

            ReceivingEndpoint = EndpointDetails.ReceivingEndpoint(message.Headers);
            SendingEndpoint = EndpointDetails.SendingEndpoint(message.Headers);

            Metadata = new Dictionary<string, MessageMetadata>();

            //add basic message metadata
            Metadata.Add("MessageId",new MessageMetadata("MessageId",message.Id));
            Metadata.Add("Headers", new MessageMetadata("Headers",message.Headers, message.Headers.Select(kvp => string.Format("{0} {1}", kvp.Key, kvp.Value)).ToArray()));
        }

        public string UniqueMessageId { get; set; }
        public string MessageId { get; set; }

        public PhysicalMessage PhysicalMessage { get; set; }

        public EndpointDetails SendingEndpoint { get; set; }

        public EndpointDetails ReceivingEndpoint { get; set; }
        public Dictionary<string, MessageMetadata> Metadata { get; set; }

        public void Add(MessageMetadata metadata)
        {
            Metadata[metadata.Name] = metadata;
        }
    }

    public class MessageMetadata
    {
        public string[] SearchTokens { get; set; }
        public object Value { get; set; }
        public string Name { get; set; }


        public MessageMetadata(string name, object value,string[] searchTokens = null)
        {
            SearchTokens = searchTokens;
            Value = value;
            Name = name;
        }
    }

    public class ImportFailedMessage : ImportMessage
    {
        public ImportFailedMessage(TransportMessage message)
            : base(message)
        {
            FailureDetails = new FailureDetails(message.Headers);
        }

        public FailureDetails FailureDetails { get; set; }

    }
}
