namespace ServiceBus.Management.Infrastructure
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using ServiceControl.Configuration;
    using ServiceControl.ExternalIntegrations;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.Settings;
    using ServiceControl.Infrastructure.Subscriptions;
    using ServiceControl.Monitoring.HeartbeatMonitoring;
    using ServiceControl.Notifications.Email;
    using ServiceControl.Operations;
    using ServiceControl.SagaAudit;
    using ServiceControl.Transports;

    static class NServiceBusFactory
    {
        public static void Configure(
            ServiceControlOptions scOptions,
            ITransportCustomization transportCustomization,
            TransportSettings transportSettings,
            EndpointConfiguration configuration)
        {
            if (configuration == null)
            {
                configuration = new EndpointConfiguration(transportSettings.EndpointName);
                var assemblyScanner = configuration.AssemblyScanner();
                assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            }

            transportCustomization.CustomizePrimaryEndpoint(configuration, transportSettings);

            configuration.GetSettings().Set(scOptions);
            configuration.SetDiagnosticsPath(scOptions.LogPath);

            if (scOptions.DisableExternalIntegrationsPublishing)
            {
                configuration.DisableFeature<ExternalIntegrationsFeature>();
            }

            var recoverability = configuration.Recoverability();
            recoverability.Immediate(c => c.NumberOfRetries(3));
            recoverability.Delayed(c => c.NumberOfRetries(0));
            recoverability.AddUnrecoverableException<UnrecoverableException>();

            configuration.SendFailedMessagesTo(transportCustomization.ToTransportQualifiedQueueName(transportSettings.ErrorQueue));

            recoverability.CustomPolicy(SendEmailNotificationHandler.RecoverabilityPolicy);

            configuration.UsePersistence<ServiceControlSubscriptionPersistence, StorageType.Subscriptions>();
            var serializer = configuration.UseSerialization<SystemJsonSerializer>();
            serializer.Options(new JsonSerializerOptions
            {
                Converters =
                {
                    new HeartbeatTypesArrayToInstanceConverter()
                },
                TypeInfoResolverChain =
                {
                    SagaAuditMessagesSerializationContext.Default,
                    HeartbeatSerializationContext.Default,
                    // This is required until we move all known message types over to source generated contexts
                    new DefaultJsonTypeInfoResolver()
                }
            });

            if (!transportSettings.MaxConcurrency.HasValue)
            {
                throw new ArgumentException("MaxConcurrency is not set in TransportSettings");
            }
            configuration.LimitMessageProcessingConcurrencyTo(transportSettings.MaxConcurrency.Value);

            configuration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));

            configuration.DefineCriticalErrorAction((criticalErrorContext, _) => CriticalErrorCustomCheck.OnCriticalError(criticalErrorContext));

            if (AppEnvironment.RunningInContainer)
            {
                // Do not write diagnostics file
                configuration.CustomDiagnosticsWriter((_, _) => Task.CompletedTask);
            }
        }

        static bool IsExternalContract(Type t) =>
            t.Namespace != null
            && t.Namespace.StartsWith("ServiceControl.Contracts")
            && t.Assembly.GetName().Name == "ServiceControl.Contracts";
    }
}