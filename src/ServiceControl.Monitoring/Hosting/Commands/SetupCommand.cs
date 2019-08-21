namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using NServiceBus;

    class SetupCommand : AbstractCommand
    {
        public override Task Execute(Settings settings)
        {
            var endpointConfig = EndpointFactory.PrepareConfiguration(settings);
            endpointConfig.EnableInstallers(settings.Username);
            if (settings.SkipQueueCreation)
            {
                endpointConfig.DoNotCreateQueues();
            }
            return Endpoint.Create(endpointConfig);
        }
    }
}