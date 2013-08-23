namespace ServiceBus.Management.Infrastructure.Heartbeats
{
    using NServiceBus;
    using ServiceControl.EndpointPlugin.Infrastructure.Heartbeats;

    public class RegisterHeartbeatMessage : IWantToRunBeforeConfiguration
    {
        public void Init()
        {
            Configure.Instance.AddSystemMessagesAs(t => t == typeof(EndpointHeartbeat));
        }
    }
}