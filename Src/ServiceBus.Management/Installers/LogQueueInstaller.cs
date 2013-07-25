namespace ServiceBus.Management.Installers
{
    using NServiceBus;
    using NServiceBus.Unicast.Queuing;

    public class LogQueueInstaller : IWantQueueCreated
    {
        public Address Address
        {
            get { return Settings.ErrorLogQueue; }
        }
        public bool IsDisabled
        {
            get
            {
                return Settings.ErrorLogQueue == Address.Undefined;
            }
        }
    }
}