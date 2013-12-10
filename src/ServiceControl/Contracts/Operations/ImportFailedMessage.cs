namespace ServiceControl.Contracts.Operations
{
    using System.Collections.Generic;
    using NServiceBus;

    public class ImportMessage : IMessage
    {
        public ImportMessage()
        {
            Properties = new Dictionary<string, MessageProperty>();
        }

        public string UniqueMessageId { get; set; }

        public PhysicalMessage PhysicalMessage { get; set; }

        public EndpointDetails SendingEndpoint { get; set; }

        public EndpointDetails ReceivingEndpoint { get; set; }
        public Dictionary<string, MessageProperty> Properties { get; set; }

        public void Add(MessageProperty property)
        {
            Properties[property.Name] = property;
        }
    }

    public class MessageProperty
    {
        public object Value { get; set; }
        public string Name { get; set; }


        public MessageProperty(string name, object value)
        {
            Value = value;
            Name = name;
        }
    }

    public class ImportFailedMessage : ImportMessage
    {

        public FailureDetails FailureDetails { get; set; }

    }
}
