namespace ServiceControl.Operations.Heartbeats
{
    using NServiceBus;
    using EndpointPlugin.Operations.Heartbeats;

    public class RegisterHeartbeatMessage : IWantToRunBeforeConfiguration
    {
        public void Init()
        {
            Configure.Instance.AddSystemMessagesAs(t => t == typeof(EndpointHeartbeat));
        }
    }
}