namespace ServiceBus.Management.Infrastructure
{
    using System;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using ServiceControl.ExternalIntegrations;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.Subscriptions;
    using ServiceControl.Notifications.Email;
    using ServiceControl.Operations;
    using ServiceControl.Transports;

    static class NServiceBusFactory
    {
        public static void Configure(Settings.Settings settings, ITransportCustomization transportCustomization,
            TransportSettings transportSettings, EndpointConfiguration configuration)
        {
            if (configuration == null)
            {
                configuration = new EndpointConfiguration(transportSettings.EndpointName);
                var assemblyScanner = configuration.AssemblyScanner();
                assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            }

            configuration.GetSettings().Set("ServiceControl.Settings", settings);

            transportCustomization.CustomizePrimaryEndpoint(configuration, transportSettings);

            configuration.GetSettings().Set(settings.LoggingSettings);
            configuration.SetDiagnosticsPath(settings.LoggingSettings.LogPath);

            if (settings.DisableExternalIntegrationsPublishing)
            {
                configuration.DisableFeature<ExternalIntegrationsFeature>();
            }

            var recoverability = configuration.Recoverability();
            recoverability.Immediate(c => c.NumberOfRetries(3));
            recoverability.Delayed(c => c.NumberOfRetries(0));
            recoverability.AddUnrecoverableException<UnrecoverableException>();

            configuration.SendFailedMessagesTo(transportSettings.ErrorQueue);

            recoverability.CustomPolicy(SendEmailNotificationHandler.RecoverabilityPolicy);

            configuration.UsePersistence<ServiceControlSubscriptionPersistence, StorageType.Subscriptions>();
            configuration.UseSerialization<NewtonsoftJsonSerializer>();

            configuration.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

            configuration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));

            configuration.DefineCriticalErrorAction((criticalErrorContext, _) => CriticalErrorCustomCheck.OnCriticalError(criticalErrorContext));
        }

        static bool IsExternalContract(Type t) =>
            t.Namespace != null
            && t.Namespace.StartsWith("ServiceControl.Contracts")
            && t.Assembly.GetName().Name == "ServiceControl.Contracts";
    }
}