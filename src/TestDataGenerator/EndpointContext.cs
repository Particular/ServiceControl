namespace TestDataGenerator
{
    using System.Threading;

    public class EndpointContext
    {
        int simpleMessagesReceived;

        public EndpointContext(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public bool FailCustomCheck { get; set; }
        public bool ThrowExceptions { get; set; }
        public int SimpleMessagesReceived => simpleMessagesReceived;

        public void LogSimpleMessage()
        {
            Interlocked.Increment(ref simpleMessagesReceived);
        }
    }
}
