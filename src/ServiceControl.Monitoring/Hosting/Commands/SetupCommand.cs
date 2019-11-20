namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;

    class SetupCommand : AbstractCommand
    {
        public override Task Execute(Settings settings)
        {
            var endpointConfig = new EndpointConfiguration(settings.EndpointName);

            new Bootstrapper(
                c => Environment.FailFast("NServiceBus Critical Error", c.Exception),
                settings,
                endpointConfig);

            endpointConfig.EnableInstallers(settings.Username);

            if (settings.SkipQueueCreation)
            {
                endpointConfig.DoNotCreateQueues();
            }

            return Endpoint.Create(endpointConfig);
        }
    }
}