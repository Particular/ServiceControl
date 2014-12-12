namespace ServiceControl.Infrastructure.Settings
{
    using NServiceBus;
    using NServiceBus.Logging;

    class SettingsCheck : IWantToRunWhenBusStartsAndStops
    {
        ILog logger = LogManager.GetLogger(typeof(SettingsCheck));

        public void Start()
        {
            if (!ServiceBus.Management.Infrastructure.Settings.Settings.ForwardAuditMessages.HasValue)
            {
                logger.WarnFormat("The setting ForwardAuditMessges is not explicitly set. To suppress this warning please set ForwardAuditMessges to true or false.");
            }
        }

        public void Stop()
        {
            //ignore
        }
    }
}
