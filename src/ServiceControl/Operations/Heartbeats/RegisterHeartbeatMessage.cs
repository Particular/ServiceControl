namespace ServiceControl.Operations.Heartbeats
{
    using EndpointPlugin.Messages.Heartbeats;
    using NServiceBus;

    public class RegisterHeartbeatMessage : IWantToRunBeforeConfiguration
    {
        public void Init()
        {
            Configure.Instance.AddSystemMessagesAs(t => t == typeof(EndpointHeartbeat));
        }
    }
}