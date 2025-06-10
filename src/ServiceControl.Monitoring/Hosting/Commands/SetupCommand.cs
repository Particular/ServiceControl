namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Transports;

    class SetupCommand(ILogger<SetupCommand> logger) : AbstractCommand
    {
        public override Task Execute(HostArguments args, Settings settings)
        {
            if (args.SkipQueueCreation)
            {
                logger.LogInformation("Skipping queue creation");
                return Task.CompletedTask;
            }

            var transportSettings = settings.ToTransportSettings();
            transportSettings.ErrorQueue = settings.ErrorQueue;
            var transportCustomization = TransportFactory.Create(transportSettings);
            return transportCustomization.ProvisionQueues(transportSettings, []);
        }
    }
}