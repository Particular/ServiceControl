namespace ServiceControl.Transports
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Transport;

    public interface ITransportCustomization
    {
        void CustomizeEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        void CustomizePrimaryEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        void CustomizeAuditEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        IProvideQueueLength CreateQueueLengthProvider();

        Type ThroughputQueryProvider { get; }

        Task ProvisionQueues(TransportSettings transportSettings, IEnumerable<string> additionalQueues);

        Task<TransportInfrastructure> CreateTransportInfrastructure(string name, TransportSettings transportSettings, OnMessage onMessage = null, OnError onError = null, Func<string, Exception, Task> onCriticalError = null, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly);
    }

    public abstract class TransportCustomization<TTransport> : ITransportCustomization where TTransport : TransportDefinition
    {
        protected abstract void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, TTransport transportDefinition, TransportSettings transportSettings);

        protected abstract void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, TTransport transportDefinition, TransportSettings transportSettings);

        protected abstract void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TTransport transportDefinition, TransportSettings transportSettings);

        public void CustomizeEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            TTransport transport = CreateTransport(transportSettings);
            endpointConfiguration.UseTransport(transport);
        }

        public void CustomizePrimaryEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            var transport = CreateTransport(transportSettings);
            endpointConfiguration.UseTransport(transport);
            CustomizeTransportForPrimaryEndpoint(endpointConfiguration, transport, transportSettings);
        }

        public void CustomizeAuditEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            var transport = CreateTransport(transportSettings);
            endpointConfiguration.UseTransport(transport);
            CustomizeTransportForAuditEndpoint(endpointConfiguration, transport, transportSettings);

            endpointConfiguration.SendOnly();

            //DisablePublishing API is available only on TransportExtensions for transports that implement IMessageDrivenPubSub so we need to set settings directly
            endpointConfiguration.GetSettings().Set("NServiceBus.PublishSubscribe.EnablePublishing", false);
        }

        public void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            var transport = CreateTransport(transportSettings);
            endpointConfiguration.UseTransport(transport);
            CustomizeTransportForMonitoringEndpoint(endpointConfiguration, transport, transportSettings);
        }

        protected void ConfigureDefaultEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            endpointConfiguration.DisableFeature<Audit>();
            endpointConfiguration.DisableFeature<AutoSubscribe>();
            endpointConfiguration.DisableFeature<Outbox>();
            endpointConfiguration.DisableFeature<Sagas>();
            endpointConfiguration.SendFailedMessagesTo(transportSettings.ErrorQueue);
        }

        public abstract IProvideQueueLength CreateQueueLengthProvider();
        public abstract Type ThroughputQueryProvider { get; }

        public virtual async Task ProvisionQueues(TransportSettings transportSettings, IEnumerable<string> additionalQueues)
        {
            var transport = CreateTransport(transportSettings);

            var hostSettings = new HostSettings(
                transportSettings.EndpointName,
                $"Queue creator for {transportSettings.EndpointName}",
                new StartupDiagnosticEntries(),
                (_, __, ___) => { },
                true,
                null); //null means "not hosted by core", transport SHOULD adjust accordingly to not assume things

            var receivers = new[]{
                new ReceiveSettings(
                    transportSettings.EndpointName,
                    new QueueAddress(transportSettings.EndpointName),
                    false,
                    false,
                    transportSettings.ErrorQueue)};

            var transportInfrastructure = await transport.Initialize(hostSettings, receivers, additionalQueues.Union(new[] { transportSettings.ErrorQueue }).ToArray());
            await transportInfrastructure.Shutdown();
        }

        public async Task<TransportInfrastructure> CreateTransportInfrastructure(string name, TransportSettings transportSettings, OnMessage onMessage = null, OnError onError = null, Func<string, Exception, Task> onCriticalError = null, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly)
        {
            var transport = CreateTransport(transportSettings, preferredTransactionMode);

            onCriticalError ??= (_, __) => Task.CompletedTask;

            var hostSettings = new HostSettings(
                name,
                $"TransportInfrastructure for {name}",
                new StartupDiagnosticEntries(),
                (msg, exception, cancellationToken) => Task.Run(() => onCriticalError(msg, exception), cancellationToken),
                false,
                null); //null means "not hosted by core", transport SHOULD adjust accordingly to not assume things


            ReceiveSettings[] receivers;
            var createReceiver = onMessage != null && onError != null;

            if (createReceiver)
            {
                receivers = new[] { new ReceiveSettings(name, new QueueAddress(name), false, false, transportSettings.ErrorQueue) };
            }
            else
            {
                receivers = Array.Empty<ReceiveSettings>();
            }

            var transportInfrastructure = await transport.Initialize(hostSettings, receivers, new[] { transportSettings.ErrorQueue });

            if (createReceiver)
            {
                var transportInfrastructureReceiver = transportInfrastructure.Receivers[name];
                await transportInfrastructureReceiver.Initialize(new PushRuntimeSettings(transportSettings.MaxConcurrency), onMessage, onError, CancellationToken.None);
            }

            return transportInfrastructure;
        }

        protected abstract TTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly);
    }
}