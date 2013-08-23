namespace ServiceBus.Management.Operations.Heartbeats
{
    using NServiceBus;
    using ServiceControl.EndpointPlugin.Operations.Heartbeats;

    public class RegisterHeartbeatMessage : IWantToRunBeforeConfiguration
    {
        public void Init()
        {
            Configure.Instance.AddSystemMessagesAs(t => t == typeof(EndpointHeartbeat));
        }
    }
}