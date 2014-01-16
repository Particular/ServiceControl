namespace ServiceBus.Management.Infrastructure.Installers
{
    using NServiceBus;
    using NServiceBus.Unicast.Queuing;
    using Settings;

    public class AuditQueueInstaller : IWantQueueCreated
    {
        public Address Address
        {
            get { return Settings.AuditLogQueue; }
        }

        public bool IsDisabled
        {
            get { return !Settings.ForwardAuditMessages; }
        }
    }
}