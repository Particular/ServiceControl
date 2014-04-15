namespace ServiceBus.Management.Infrastructure.Installers
{
    using NServiceBus;
    using NServiceBus.Unicast.Queuing;
    using Settings;

    public class AuditQueueInstaller : IWantQueueCreated
    {
        public Address Address
        {
            get { return Settings.AuditQueue; }
        }

        public bool IsDisabled
        {
            get { return Settings.AuditQueue == Address.Undefined; }
        }
    }
}