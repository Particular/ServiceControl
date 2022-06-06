namespace ServiceBus.Management.Infrastructure
{
    using System;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.RavenDB.Subscriptions;
    using ServiceControl.Notifications.Email;
    using ServiceControl.Operations;
    using ServiceControl.Transports;
    using Settings;

    static class NServiceBusFactory
    {
        public static void Configure(Settings.Settings settings, TransportCustomization transportCustomization, TransportSettings transportSettings, LoggingSettings loggingSettings, EndpointConfiguration configuration)
        {
            var endpointName = settings.ServiceName;
            if (configuration == null)
            {
                configuration = new EndpointConfiguration(endpointName);
                var assemblyScanner = configuration.AssemblyScanner();
                assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            }

            configuration.GetSettings().Set("ServiceControl.Settings", settings);

            transportCustomization.CustomizeServiceControlEndpoint(configuration, transportSettings);

            configuration.GetSettings().Set(loggingSettings);
            configuration.SetDiagnosticsPath(loggingSettings.LogPath);

            // Disable Auditing for the service control endpoint
            configuration.DisableFeature<Audit>();
            configuration.DisableFeature<AutoSubscribe>();
            configuration.DisableFeature<TimeoutManager>();
            configuration.DisableFeature<Outbox>();
            configuration.DisableFeature<Sagas>();

            configuration.Pipeline.Register(new RemoveVersionInformationBehavior(),
                "Removes version information from ServiceControl.Contracts messages");

            var recoverability = configuration.Recoverability();
            recoverability.Immediate(c => c.NumberOfRetries(3));
            recoverability.Delayed(c => c.NumberOfRetries(0));
            configuration.SendFailedMessagesTo($"{endpointName}.Errors");

            recoverability.CustomPolicy(SendEmailNotificationHandler.RecoverabilityPolicy);

            configuration.UsePersistence<CachedRavenDBPersistence, StorageType.Subscriptions>();
            configuration.UseSerialization<NewtonsoftSerializer>();

            configuration.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

            configuration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));
            configuration.Conventions().Conventions.AddSystemMessagesConventions(t => t.Namespace != null
                                                                          && t.Namespace.StartsWith("ServiceControl.Plugin.")
                                                                          && t.Namespace.EndsWith(".Messages"));

            configuration.DefineCriticalErrorAction(CriticalErrorCustomCheck.OnCriticalError);
        }

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null
                   && t.Namespace.StartsWith("ServiceControl.Contracts")
                   && t.Assembly.GetName().Name == "ServiceControl.Contracts";
        }
    }
}