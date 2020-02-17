namespace ServiceControl.Audit.Infrastructure
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using NServiceBus.Raw;
    using Transports;

    class SetupBootstrapper
    {
        public SetupBootstrapper(Settings.Settings settings)
        {
            this.settings = settings;
        }

        public async Task Run(string username)
        {
            var transportSettings = MapSettings(settings);
            var transportCustomization = settings.LoadTransportCustomization();
            var factory = new RawEndpointFactory(settings, transportSettings, transportCustomization);

            var config = factory.CreateRawEndpointConfiguration(settings.AuditQueue, (context, dispatcher) => Task.CompletedTask);

            if (settings.SkipQueueCreation)
            {
                log.Info("Skipping queue creation");
            }
            else
            {
                var additionalQueues = new List<string>
                {
                    $"{settings.ServiceName}.Errors"
                };
                if (settings.ForwardAuditMessages && settings.AuditLogQueue != null)
                {
                    additionalQueues.Add(settings.AuditLogQueue);
                }
                config.AutoCreateQueues(additionalQueues.ToArray(), username);
            }

            //No need to start the raw endpoint to create queues
            await RawEndpoint.Create(config).ConfigureAwait(false);
        }

        static TransportSettings MapSettings(Settings.Settings settings)
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = settings.ServiceName,
                ConnectionString = settings.TransportConnectionString,
                MaxConcurrency = settings.MaximumConcurrencyLevel
            };
            return transportSettings;
        }

        private readonly Settings.Settings settings;

        private static ILog log = LogManager.GetLogger<SetupBootstrapper>();
    }
}