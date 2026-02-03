namespace ServiceControl.Transports.Learning
{
    using System;
    using System.IO;
    using System.Linq;
    using LearningTransport;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;

    public class LearningTransportCustomization : TransportCustomization<LearningTransport>
    {
        protected override void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, LearningTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;

        protected override void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, LearningTransport transportDefinition, TransportSettings transportSettings) =>
        transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, LearningTransport transportDefinition, TransportSettings transportSettings) =>
            transportDefinition.TransportTransactionMode = TransportTransactionMode.ReceiveOnly;

        protected override LearningTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            var transport = new LearningTransport
            {
                StorageDirectory = FindStoragePath(transportSettings.ConnectionString),
                RestrictPayloadSize = false,
            };

            transport.TransportTransactionMode = transport.GetSupportedTransactionModes().Contains(preferredTransactionMode) ? preferredTransactionMode : TransportTransactionMode.ReceiveOnly;

            return transport;
        }

        protected override void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
        {
            services.AddSingleton<IProvideQueueLength, QueueLengthProvider>();
            services.AddHostedService(provider => provider.GetRequiredService<IProvideQueueLength>());
        }

        internal static string FindStoragePath(string connectionString)
        {
            var storagePath = Environment.ExpandEnvironmentVariables(connectionString);

            if (!string.IsNullOrWhiteSpace(storagePath))
            {
                return storagePath;
            }

            var directory = AppDomain.CurrentDomain.BaseDirectory;

            while (true)
            {
                if (Directory.EnumerateFiles(directory).Any(file => file.EndsWith(".sln") || file.EndsWith(".slnx")))
                {
                    return Path.Combine(directory, ".learningtransport");
                }

                var parent = Directory.GetParent(directory) ?? throw new Exception($"Unable to determine the storage directory path for the learning transport due to the absence of a solution file. Specify the path explicitly in the app.config connection string.");

                directory = parent.FullName;
            }
        }
    }
}