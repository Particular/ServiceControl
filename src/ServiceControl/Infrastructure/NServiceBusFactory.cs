namespace ServiceBus.Management.Infrastructure
{
    using System;
    using System.Diagnostics;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using ServiceControl.CustomChecks;
    using ServiceControl.ExternalIntegrations;
    using ServiceControl.Notifications.Email;
    using ServiceControl.Operations;
    using ServiceControl.Transports;
    using Settings;

    static class NServiceBusFactory
    {
        public static void Configure(Settings.Settings settings, TransportCustomization transportCustomization, TransportSettings transportSettings, LoggingSettings loggingSettings, EndpointConfiguration configuration, bool isRunningAcceptanceTests)
        {
            var endpointName = settings.ServiceName;
            if (configuration == null)
            {
                configuration = new EndpointConfiguration(endpointName);
                var assemblyScanner = configuration.AssemblyScanner();
                assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            }

            // HACK: Yes I know, I am hacking it to pass it to RavenBootstrapper!
            // wrapping it in a non-disposable type to make sure settings clear doesn't dispose
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

            if (settings.DisableExternalIntegrationsPublishing)
            {
                configuration.DisableFeature<ExternalIntegrationsFeature>();
            }

            var recoverability = configuration.Recoverability();
            recoverability.Immediate(c => c.NumberOfRetries(3));
            recoverability.Delayed(c => c.NumberOfRetries(0));
            configuration.SendFailedMessagesTo($"{endpointName}.Errors");

            recoverability.CustomPolicy(SendEmailNotificationHandler.RecoverabilityPolicy);

            configuration.UseSerialization<NewtonsoftSerializer>();

            configuration.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

            configuration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));

            if (!isRunningAcceptanceTests)
            {
                configuration.EnableFeature<InternalCustomChecks>();
            }

            configuration.DefineCriticalErrorAction(CriticalErrorCustomCheck.OnCriticalError);

            if (Environment.UserInteractive && Debugger.IsAttached)
            {
                configuration.EnableInstallers();
            }
        }

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null
                   && t.Namespace.StartsWith("ServiceControl.Contracts")
                   && t.Assembly.GetName().Name == "ServiceControl.Contracts";
        }
    }
}