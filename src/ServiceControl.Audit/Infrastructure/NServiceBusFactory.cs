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
    using Settings;
    using Transports;

    static class NServiceBusFactory
    {
        public static void Configure(Settings.Settings settings, TransportCustomization transportCustomization, TransportSettings transportSettings, LoggingSettings loggingSettings, Action<ICriticalErrorContext> onCriticalError, EndpointConfiguration configuration, bool isRunningAcceptanceTests)
        {
            var endpointName = settings.ServiceName;
            if (configuration == null)
            {
                configuration = new EndpointConfiguration(endpointName);
                var assemblyScanner = configuration.AssemblyScanner();
                assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            }

            configuration.Pipeline.Register(typeof(FullTypeNameOnlyBehavior), "Remove asm qualified name from the message type header");

            configuration.GetSettings().Set("ServiceControl.Settings", settings);

            configuration.SendOnly();

            transportCustomization.CustomizeSendOnlyEndpoint(configuration, transportSettings);
            //DisablePublishing API is available only on TransportExtensions for transports that implement IMessageDrivenPubSub so we need to set settings directly
            configuration.GetSettings().Set("NServiceBus.PublishSubscribe.EnablePublishing", false);

            var serviceControlLogicalQueue = settings.ServiceControlQueueAddress;
            if (serviceControlLogicalQueue.IndexOf("@") >= 0)
            {
                serviceControlLogicalQueue = serviceControlLogicalQueue.Substring(0, serviceControlLogicalQueue.IndexOf("@"));
            }

            var routing = new RoutingSettings(configuration.GetSettings());
            routing.RouteToEndpoint(typeof(RegisterNewEndpoint), serviceControlLogicalQueue);
            routing.RouteToEndpoint(typeof(MarkMessageFailureResolvedByRetry), serviceControlLogicalQueue);

            configuration.GetSettings().Set(loggingSettings);
            configuration.SetDiagnosticsPath(loggingSettings.LogPath);

            // sagas are not auto-disabled for send-only endpoints
            configuration.DisableFeature<Sagas>();

            configuration.UseSerialization<NewtonsoftJsonSerializer>();

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