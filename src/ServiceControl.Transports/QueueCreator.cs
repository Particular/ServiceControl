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
            var endpointConfiguration = new EndpointConfiguration(transportSettings.EndpointName);

            customization(endpointConfiguration, transportSettings);

            var settings = endpointConfiguration.GetSettings();
            var transportDefinition = settings.Get<TransportDefinition>();

            var connectionString = transportDefinition.GetType().Name.Contains("SqsTransport")
                ? string.Empty
                : transportSettings.ConnectionString;

            var transportInfrastructure = transportDefinition.Initialize(settings, connectionString);

            var receiveInfrastructure = transportInfrastructure.ConfigureReceiveInfrastructure();
            var queueCreator = receiveInfrastructure.QueueCreatorFactory();

            var queueBindings = new QueueBindings();
            foreach (var queue in queues)
            {
                queueBindings.BindSending(queue);
            }

            return queueCreator.CreateQueueIfNecessary(queueBindings, GetInstallationUserName(userName));
        }

        //HINT: this is recreating NServiceBus core behavior which makes sure we never pass a null user identity to transport queue creation
        static string GetInstallationUserName(string userName)
        {
            if (userName != null)
            {
                return userName;
            }

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return $"{Environment.UserDomainName}\\{Environment.UserName}";
            }

            return Environment.UserName;
        }
    }
}