namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using Contracts.MessageFailures;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using Raven.Client.Embedded;
    using RavenDB;
    using Settings;
    using Transports;

    static class NServiceBusFactory
    {
        public static Task<IStartableEndpoint> Create(Settings.Settings settings, TransportCustomization transportCustomization, TransportSettings transportSettings, LoggingSettings loggingSettings, Action<ICriticalErrorContext> onCriticalError, EmbeddableDocumentStore documentStore, EndpointConfiguration configuration, bool isRunningAcceptanceTests)
        {
            Configure(settings, transportCustomization, transportSettings, loggingSettings, onCriticalError, documentStore, configuration, isRunningAcceptanceTests);

            return Endpoint.Create(configuration);
        }

        public static void Configure(Settings.Settings settings, TransportCustomization transportCustomization, TransportSettings transportSettings, LoggingSettings loggingSettings, Action<ICriticalErrorContext> onCriticalError, EmbeddableDocumentStore documentStore, EndpointConfiguration configuration, bool isRunningAcceptanceTests)
        {
            var endpointName = settings.ServiceName;
            if (configuration == null)
            {
                configuration = new EndpointConfiguration(endpointName);
                var assemblyScanner = configuration.AssemblyScanner();
                assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            }

            configuration.Pipeline.Register(typeof(FullTypeNameOnlyBehavior), "Remove asm qualified name from the message type header");

            // HACK: Yes I know, I am hacking it to pass it to RavenBootstrapper!
            // wrapping it in a non-disposable type to make sure settings clear doesn't dispose
            configuration.GetSettings().Set(new EmbeddableDocumentStoreHolder(documentStore));
            configuration.GetSettings().Set("ServiceControl.Settings", settings);

            configuration.SendOnly();

            transportCustomization.CustomizeSendOnlyEndpoint(configuration, transportSettings);
            //DisablePublishing API is available only on TransportExtensions for transports that implement IMessageDrivenPubSub so we need to set settings directly
            configuration.GetSettings().Set("NServiceBus.PublishSubscribe.EnablePublishing", false);

            var routing = new RoutingSettings(configuration.GetSettings());
            routing.RouteToEndpoint(typeof(RegisterNewEndpoint), settings.ServiceControlQueueAddress);
            routing.RouteToEndpoint(typeof(MarkMessageFailureResolvedByRetry), settings.ServiceControlQueueAddress);

            configuration.GetSettings().Set(loggingSettings);
            configuration.SetDiagnosticsPath(loggingSettings.LogPath);

            // sagas are not auto-disabled for send-only endpoints
            configuration.DisableFeature<Sagas>();

            configuration.UseSerialization<NewtonsoftSerializer>();

            configuration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));

            if (!isRunningAcceptanceTests)
            {
                configuration.ReportCustomChecksTo(settings.ServiceControlQueueAddress);
            }

            configuration.DefineCriticalErrorAction(criticalErrorContext =>
            {
                onCriticalError(criticalErrorContext);
                return Task.FromResult(0);
            });

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