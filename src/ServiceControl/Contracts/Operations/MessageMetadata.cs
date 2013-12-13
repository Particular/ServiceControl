namespace ServiceControl.Contracts.Operations
{
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
}