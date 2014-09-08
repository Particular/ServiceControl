namespace ServiceBus.Management.Infrastructure.Installers
{
    using NServiceBus;
    using NServiceBus.Unicast.Queuing;
    using Settings;

    public class AuditLogQueueInstaller : IWantQueueCreated
    {
        public bool ShouldCreateQueue()
        {
            return Settings.ForwardAuditMessages;
        }

        public Address Address
        {
            get { return Settings.AuditLogQueue; }
        }
    }
}