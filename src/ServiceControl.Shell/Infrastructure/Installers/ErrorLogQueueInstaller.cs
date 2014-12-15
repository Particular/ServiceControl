namespace ServiceBus.Management.Infrastructure.Installers
{
    using NServiceBus;
    using NServiceBus.Unicast.Queuing;
    using Settings;

    public class ErrorLogQueueInstaller : IWantQueueCreated
    {
        public Address Address
        {
            get { return Settings.ErrorLogQueue; }
        }

        public bool IsDisabled
        {
            get { return Settings.ErrorLogQueue == Address.Undefined; }
        }
    }
}