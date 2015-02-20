namespace ServiceBus.Management.Infrastructure.Installers
{
    using NServiceBus;
    using NServiceBus.Unicast.Queuing;
    using ServiceBus.Management.Infrastructure.Settings;

    public class AuditImportFailureQueueInstaller : IWantQueueCreated
    {
        public Address Address
        {
            get { return Settings.AuditImportFailureQueue; }
        }

        public bool IsDisabled
        {
            get { return Settings.AuditLogQueue == Address.Undefined; }
        }
    }
}