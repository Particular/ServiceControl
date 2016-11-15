namespace ServiceBus.Management.Infrastructure.Installers
{
    using NServiceBus;
    using NServiceBus.Unicast.Queuing;
    using Settings;

    public class ErrorLogQueueInstaller : IWantQueueCreated
    {
        public Settings Settings { get; set; }

        public bool ShouldCreateQueue()
        {
            return Settings.ForwardErrorMessages && Settings.ErrorLogQueue != Address.Undefined;
        }

        public Address Address => Settings.ErrorLogQueue;
    }
}