namespace ServiceControl.Contracts.Operations
{
    public class MessageMetadata
    {
        public object Value { get; set; }
        public string Name { get; set; }

        public MessageMetadata(string name, object value)
        {
            Value = value;
            Name = name;
        }
    }
}