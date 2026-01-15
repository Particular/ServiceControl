namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using Contracts.MessageFailures;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using ServiceControl.Configuration;
    using ServiceControl.Infrastructure;
    using Transports;

    static class NServiceBusFactory
    {
        public static void Configure(Settings.Settings settings, ITransportCustomization transportCustomization,
            TransportSettings transportSettings,
            Func<ICriticalErrorContext, CancellationToken, Task> onCriticalError, EndpointConfiguration configuration)
        {
            var endpointName = settings.InstanceName;
            if (configuration == null)
            {
                configuration = new EndpointConfiguration(endpointName);
                var assemblyScanner = configuration.AssemblyScanner();
                assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            }

            configuration.Pipeline.Register(typeof(FullTypeNameOnlyBehavior), "Remove asm qualified name from the message type header");

            configuration.GetSettings().Set("ServiceControl.Settings", settings);

            transportCustomization.CustomizeAuditEndpoint(configuration, transportSettings);

            var serviceControlLogicalQueue = settings.ServiceControlQueueAddress;
            if (!string.IsNullOrWhiteSpace(serviceControlLogicalQueue))
            {
                if (serviceControlLogicalQueue.IndexOf("@") >= 0)
                {
                    serviceControlLogicalQueue = serviceControlLogicalQueue.Substring(0, serviceControlLogicalQueue.IndexOf("@"));
                }

                var routing = new RoutingSettings(configuration.GetSettings());
                routing.RouteToEndpoint(typeof(RegisterNewEndpoint), serviceControlLogicalQueue);
                routing.RouteToEndpoint(typeof(MarkMessageFailureResolvedByRetry), serviceControlLogicalQueue);

                configuration.ReportCustomChecksTo(transportCustomization.ToTransportQualifiedQueueName(settings.ServiceControlQueueAddress), TimeSpan.FromMinutes(1));
            }

            configuration.GetSettings().Set(settings.LoggingSettings);
            configuration.SetDiagnosticsPath(settings.LoggingSettings.LogPath);

            configuration.UseSerialization<SystemJsonSerializer>();

            configuration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));

            configuration.DefineCriticalErrorAction(onCriticalError);

            configuration.Recoverability().AddUnrecoverableException<UnrecoverableException>();

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