namespace ServiceBus.Management.Infrastructure.Installers
{
    using NServiceBus;
    using NServiceBus.Unicast.Queuing;
    using Settings;

    public class AuditQueueInstaller : IWantQueueCreated
    {
        public Settings Settings { get; set; }

        public bool ShouldCreateQueue()
        {
            return Settings.AuditQueue != Address.Undefined;
        }

        public Address Address => Settings.AuditQueue;
    }
}