namespace ServiceBus.Management.Infrastructure.Installers
{
    using NServiceBus;
    using NServiceBus.Unicast.Queuing;
    using Settings;

    public class ErrorLogQueueInstaller : IWantQueueCreated
    {
        public bool ShouldCreateQueue()
        {
            return Settings.ErrorLogQueue != Address.Undefined;
        }

        public Address Address => Settings.ErrorLogQueue;
    }
}