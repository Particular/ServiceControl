namespace ServiceControl.Monitoring.Infrastructure
{
    public class MessageTypeRegistry : BreakdownRegistry<EndpointMessageType>
    {
        public MessageTypeRegistry() : base(i => i.EndpointName)
        {
        }
    }
}