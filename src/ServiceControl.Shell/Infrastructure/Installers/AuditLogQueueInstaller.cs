namespace ServiceBus.Management.Infrastructure.Installers
{
    using NServiceBus;
    using NServiceBus.Unicast.Queuing;
    using Settings;

    public class AuditLogQueueInstaller : IWantQueueCreated
    {
        public Address Address
        {
            get { return Settings.AuditLogQueue; }
        }

        public bool IsDisabled
        {
            get { return Settings.AuditLogQueue == Address.Undefined; }
        }
    }
}