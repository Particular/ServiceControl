namespace ServiceControl.Transports
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Transport;

    public static class QueueCreator
    {
        /// <summary>
        /// Ensures specified queues exist.
        /// </summary>
        public static Task CreateQueues(TransportSettings transportSettings, Action<EndpointConfiguration, TransportSettings> customization, string userName, params string[] queues)
        {
            var endpointConfiguration = new EndpointConfiguration("QueueCreator");

            customization(endpointConfiguration, transportSettings);

            var settings = endpointConfiguration.GetSettings();
            var transportDefinition = settings.Get<TransportDefinition>();
            var transportInfrastructure = transportDefinition.Initialize(settings, transportSettings.ConnectionString);

            var receiveInfrastructure = transportInfrastructure.ConfigureReceiveInfrastructure();
            var queueCreator = receiveInfrastructure.QueueCreatorFactory();

            var queueBindings = new QueueBindings();
            foreach (var queue in queues)
            {
                queueBindings.BindSending(queue);
            }

            return queueCreator.CreateQueueIfNecessary(queueBindings, userName);
        }
    }
}