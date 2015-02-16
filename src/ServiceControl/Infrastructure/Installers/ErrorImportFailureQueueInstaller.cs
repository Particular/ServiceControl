namespace ServiceBus.Management.Infrastructure.Installers
{
    using NServiceBus;
    using NServiceBus.Unicast.Queuing;
    using ServiceBus.Management.Infrastructure.Settings;

    public class ErrorImportFailureQueueInstaller : IWantQueueCreated
    {
        public Address Address
        {
            get { return Settings.ErrorImportFailureQueue; }
        }

        public bool IsDisabled
        {
            get { return Settings.ErrorLogQueue == Address.Undefined; }
        }
    }
}