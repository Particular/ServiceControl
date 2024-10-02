namespace ServiceControl.Transports
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Transport;

    public interface ITransportCustomization
    {
        void CustomizePrimaryEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);
        void AddTransportForPrimary(IServiceCollection services, TransportSettings transportSettings);

        void CustomizeAuditEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);
        void AddTransportForAudit(IServiceCollection services, TransportSettings transportSettings);

        void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings);
        void AddTransportForMonitoring(IServiceCollection services, TransportSettings transportSettings);

        Task ProvisionQueues(TransportSettings transportSettings, IEnumerable<string> additionalQueues);
        string ToTransportQualifiedQueueName(string queueName);

        Task<TransportInfrastructure> CreateTransportInfrastructure(string name, TransportSettings transportSettings, OnMessage onMessage = null, OnError onError = null, Func<string, Exception, Task> onCriticalError = null, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly);
    }

    public abstract class TransportCustomization<TTransport> : ITransportCustomization where TTransport : TransportDefinition
    {
        protected abstract void CustomizeTransportForPrimaryEndpoint(EndpointConfiguration endpointConfiguration, TTransport transportDefinition, TransportSettings transportSettings);

        protected abstract void CustomizeTransportForAuditEndpoint(EndpointConfiguration endpointConfiguration, TTransport transportDefinition, TransportSettings transportSettings);

        protected abstract void CustomizeTransportForMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TTransport transportDefinition, TransportSettings transportSettings);

        public void AddTransportForPrimary(IServiceCollection services, TransportSettings transportSettings)
        {
            services.AddSingleton<ITransportCustomization>(this);
            services.AddSingleton(transportSettings);

            AddTransportForPrimaryCore(services, transportSettings);
        }

        protected virtual void AddTransportForPrimaryCore(IServiceCollection services, TransportSettings transportSettings)
        {
        }

        public void AddTransportForAudit(IServiceCollection services, TransportSettings transportSettings)
        {
            services.AddSingleton<ITransportCustomization>(this);
            services.AddSingleton(transportSettings);

            AddTransportForAuditCore(services, transportSettings);
        }

        protected virtual void AddTransportForAuditCore(IServiceCollection services, TransportSettings transportSettings)
        {
        }

        public void AddTransportForMonitoring(IServiceCollection services, TransportSettings transportSettings)
        {
            services.AddSingleton<ITransportCustomization>(this);
            services.AddSingleton(transportSettings);

            AddTransportForMonitoringCore(services, transportSettings);
        }

        protected virtual void AddTransportForMonitoringCore(IServiceCollection services, TransportSettings transportSettings)
        {
        }

        public void CustomizePrimaryEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            var transport = CreateTransport(transportSettings);
            CustomizeTransportForPrimaryEndpoint(endpointConfiguration, transport, transportSettings);

            transportSettings.MaxConcurrency ??= 10;

            endpointConfiguration.UseTransport(transport);
        }

        public void CustomizeAuditEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            var transport = CreateTransport(transportSettings);
            CustomizeTransportForAuditEndpoint(endpointConfiguration, transport, transportSettings);

            transportSettings.MaxConcurrency ??= 32;

            endpointConfiguration.SendOnly();

            //DisablePublishing API is available only on TransportExtensions for transports that implement IMessageDrivenPubSub so we need to set settings directly
            endpointConfiguration.GetSettings().Set("NServiceBus.PublishSubscribe.EnablePublishing", false);

            endpointConfiguration.UseTransport(transport);
        }

        public void CustomizeMonitoringEndpoint(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            ConfigureDefaultEndpointSettings(endpointConfiguration, transportSettings);
            var transport = CreateTransport(transportSettings);
            CustomizeTransportForMonitoringEndpoint(endpointConfiguration, transport, transportSettings);

            transportSettings.MaxConcurrency ??= 32;

            endpointConfiguration.UseTransport(transport);
        }

        protected void ConfigureDefaultEndpointSettings(EndpointConfiguration endpointConfiguration, TransportSettings transportSettings)
        {
            endpointConfiguration.DisableFeature<Audit>();
            endpointConfiguration.DisableFeature<AutoSubscribe>();
            endpointConfiguration.DisableFeature<Outbox>();
            endpointConfiguration.DisableFeature<Sagas>();
            endpointConfiguration.SendFailedMessagesTo(transportSettings.ErrorQueue);
        }

        public string ToTransportQualifiedQueueName(string queueName) => ToTransportQualifiedQueueNameCore(queueName);

        protected virtual string ToTransportQualifiedQueueNameCore(string queueName) => queueName;

        public virtual async Task ProvisionQueues(TransportSettings transportSettings, IEnumerable<string> additionalQueues)
        {
            // For queue provisioning MaxConcurrency is not relevant. Settings it to 1 to avoid null checks
            transportSettings.MaxConcurrency ??= 1;

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

            var transportInfrastructure = await transport.Initialize(hostSettings, receivers, additionalQueues.Union([transportSettings.ErrorQueue]).Select(ToTransportQualifiedQueueNameCore).ToArray());
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
                receivers = [new ReceiveSettings(name, new QueueAddress(name), false, false, transportSettings.ErrorQueue)];
            }
            else
            {
                receivers = [];
            }

            var transportInfrastructure = await transport.Initialize(hostSettings, receivers, new[] { ToTransportQualifiedQueueNameCore(transportSettings.ErrorQueue) });

            if (createReceiver)
            {
                if (!transportSettings.MaxConcurrency.HasValue)
                {
                    throw new ArgumentException("MaxConcurrency is not set in TransportSettings");
                }

                var transportInfrastructureReceiver = transportInfrastructure.Receivers[name];
                await transportInfrastructureReceiver.Initialize(new PushRuntimeSettings(transportSettings.MaxConcurrency.Value), onMessage, onError, CancellationToken.None);
            }

            return transportInfrastructure;
        }

        protected abstract TTransport CreateTransport(TransportSettings transportSettings, TransportTransactionMode preferredTransactionMode = TransportTransactionMode.ReceiveOnly);
    }
}