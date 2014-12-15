namespace ServiceControl.Contracts.Operations
{
    using System.Collections.Generic;
    using NServiceBus;

    public class ImportMessage : IMessage
    {
        const string Separator = " ";

        protected ImportMessage(TransportMessage message, string uniqueId)
        {
            UniqueMessageId = uniqueId;
            MessageId = message.Id;
            
            PhysicalMessage = new PhysicalMessage(message);
        
            Metadata = new Dictionary<string, object>
            {
                {"MessageId", message.Id},
                {"MessageIntent", message.MessageIntent},
                {"HeadersForSearching", string.Join(Separator, message.Headers.Values)}
            };

            //add basic message metadata
        }

        public string UniqueMessageId { get; set; }

        public string MessageId { get; set; }

        public PhysicalMessage PhysicalMessage { get; set; }

        public Dictionary<string, object> Metadata { get; set; }
    }
}