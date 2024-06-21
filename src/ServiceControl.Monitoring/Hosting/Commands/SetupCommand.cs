namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Transports;

    class SetupCommand : AbstractCommand
    {
        public override Task Execute(Settings settings)
        {
            if (settings.SkipQueueCreation)
            {
                Logger.Info("Skipping queue creation");
                return Task.CompletedTask;
            }

            var transportSettings = settings.ToTransportSettings();
            transportSettings.ErrorQueue = settings.ErrorQueue;
            var transportCustomization = TransportFactory.Create(transportSettings);
            return transportCustomization.ProvisionQueues(transportSettings, []);
        }

        static readonly ILog Logger = LogManager.GetLogger<SetupCommand>();
    }
}