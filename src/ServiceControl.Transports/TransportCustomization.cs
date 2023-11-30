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
        Task<TransportInfrastructure> CreateRawEndpointForReturnToSenderIngestion(string name, TransportSettings transportSettings, OnMessage onMessage, OnError onError, Func<string, Exception, Task> onCriticalError);

        void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);

        IProvideQueueLength CreateQueueLengthProvider();

        Task<IMessageDispatcher> InitializeDispatcher(string name, TransportSettings transportSettings);

        Task<TransportInfrastructure> CreateRawEndpointForIngestion(
            string queueName,
            TransportSettings transportSettings,
            OnMessage onMessage,
            OnError onError,
            Func<string, Exception, Task> onCriticalError);

        Task ProvisionQueues(TransportSettings transportSettings, IEnumerable<string> additionalQueues);
    }

    public abstract class TransportCustomization<TTransport> : ITransportCustomization where TTransport : TransportDefinition
    {
        protected abstract void CustomizeTransportSpecificServiceControlEndpointSettings(
            EndpointConfiguration endpointConfiguration, TTransport transportDefinition,
            TransportSettings transportSettings);

        protected abstract void CustomizeTransportSpecificSendOnlyEndpointSettings(EndpointConfiguration endpointConfiguration, TTransport transportDefinition, TransportSettings transportSettings);

        protected abstract void CustomizeTransportSpecificMonitoringEndpointSettings(EndpointConfiguration endpointConfiguration, TTransport transportDefinition, TransportSettings transportSettings);

        protected abstract void CustomizeForReturnToSenderIngestion(TTransport transportDefinition, TransportSettings transportSettings);

        public async Task<TransportInfrastructure> CreateRawEndpointForReturnToSenderIngestion(string name, TransportSettings transportSettings, OnMessage onMessage, OnError onError, Func<string, Exception, Task> onCriticalError)
        {
            var transport = CreateTransport(transportSettings);

            CustomizeForReturnToSenderIngestion(transport, transportSettings);

            var hostSettings = new HostSettings(
                name,
                "NServiceBus.Raw host for " + name,
                new StartupDiagnosticEntries(),
                (msg, exception, cancellationToken) =>
                {
                    // Mimic raw fire and forget
                    _ = Task.Run(() => onCriticalError(msg, exception), cancellationToken);
                },
                false, // ???
                null); //null means "not hosted by core", transport SHOULD adjust accordingly to not assume things

            var receivers = new[]{
                new ReceiveSettings(
                name,
                new QueueAddress(name),
                false,
                false,
                transportSettings.ErrorQueue)};

            var transportInfrastructure = await transport.Initialize(hostSettings, receivers, new[] { transportSettings.ErrorQueue });
            IMessageReceiver transportInfrastructureReceiver = transportInfrastructure.Receivers[name];
            await transportInfrastructureReceiver.Initialize(new PushRuntimeSettings(transportSettings.MaxConcurrency), onMessage, onError, CancellationToken.None);
            return transportInfrastructure;
        }

        public void CustomizeServiceControlEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            var transport = CreateTransport(transportSettings);
            endpointConfiguration.UseTransport(transport);
            CustomizeTransportSpecificServiceControlEndpointSettings(endpointConfiguration, transport, transportSettings);
        }

        public void CustomizeSendOnlyEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            var transport = CreateTransport(transportSettings);
            endpointConfiguration.UseTransport(transport);
            CustomizeTransportSpecificSendOnlyEndpointSettings(endpointConfiguration, transport, transportSettings);

            endpointConfiguration.SendOnly();

            //DisablePublishing API is available only on TransportExtensions for transports that implement IMessageDrivenPubSub so we need to set settings directly
            endpointConfiguration.GetSettings().Set("NServiceBus.PublishSubscribe.EnablePublishing", false);
        }

        public void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            var transport = CreateTransport(transportSettings);
            endpointConfiguration.UseTransport(transport);
            CustomizeTransportSpecificMonitoringEndpointSettings(endpointConfiguration, transport, transportSettings);
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

        public async Task<IMessageDispatcher> InitializeDispatcher(string name, TransportSettings transportSettings)
        {
            var transport = CreateTransport(transportSettings);
            CustomizeRawSendOnlyEndpoint(transport, transportSettings);

            var hostSettings = new HostSettings(
                name,
                $"Dispatcher for {name}",
                new StartupDiagnosticEntries(),
                (_, __, ___) => { },
                false,
                null); //null means "not hosted by core", transport SHOULD adjust accordingly to not assume things

            var transportInfrastructure = await transport.Initialize(hostSettings, Array.Empty<ReceiveSettings>(), Array.Empty<string>());
            return transportInfrastructure.Dispatcher;
        }

        public async Task<TransportInfrastructure> CreateRawEndpointForIngestion(
            string queueName,
            TransportSettings transportSettings,
            OnMessage onMessage,
            OnError onError,
            Func<string, Exception, Task> onCriticalError)
        {
            var transport = CreateTransport(transportSettings);

            CustomizeForQueueIngestion(transport, transportSettings);

            var hostSettings = new HostSettings(
                queueName,
                "NServiceBus.Raw host for " + queueName,
                new StartupDiagnosticEntries(),
                (msg, exception, cancellationToken) =>
                {
                    // Mimic raw fire and forget
                    _ = Task.Run(() => onCriticalError(msg, exception), cancellationToken);
                },
                false, // ???
                null); //null means "not hosted by core", transport SHOULD adjust accordingly to not assume things

            var receivers = new[]{
                new ReceiveSettings(
                    queueName,
                    new QueueAddress(queueName),
                    false,
                    false,
                    transportSettings.ErrorQueue)};

            var transportInfrastructure = await transport.Initialize(hostSettings, receivers, new[] { transportSettings.ErrorQueue });
            IMessageReceiver transportInfrastructureReceiver = transportInfrastructure.Receivers[queueName];
            await transportInfrastructureReceiver.Initialize(new PushRuntimeSettings(transportSettings.MaxConcurrency), onMessage, onError, CancellationToken.None);
            return transportInfrastructure;
        }

        public virtual async Task ProvisionQueues(TransportSettings transportSettings, IEnumerable<string> additionalQueues)
        {
            var transport = CreateTransport(transportSettings);

            CustomizeForQueueIngestion(transport, transportSettings);

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
            // TODO NSB8 finally block?
            await transportInfrastructure.Shutdown();
        }

        protected abstract void CustomizeRawSendOnlyEndpoint(TTransport transportDefinition, TransportSettings transportSettings);

        protected abstract void CustomizeForQueueIngestion(TTransport transportDefinition, TransportSettings transportSettings);

        protected abstract TTransport CreateTransport(TransportSettings transportSettings);
    }
}