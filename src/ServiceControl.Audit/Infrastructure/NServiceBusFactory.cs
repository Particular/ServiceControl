namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Contracts.EndpointControl;
    using Contracts.MessageFailures;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using Plugins;
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
                assemblyScanner.Disable = true;
            }

            configuration.EnableFeature<RegisterPluginMessagesFeature>();

            configuration.Pipeline.Register(typeof(FullTypeNameOnlyBehavior), "Remove asm qualified name from the message type header");

            configuration.GetSettings().Set("ServiceControl.Settings", settings);

            transportCustomization.CustomizeAuditEndpoint(configuration, transportSettings);

            var serviceControlLogicalQueue = settings.ServiceControlQueueAddress;
            if (!string.IsNullOrWhiteSpace(serviceControlLogicalQueue))
            {
                var indexOfAtSign = serviceControlLogicalQueue.IndexOf("@", StringComparison.Ordinal);
                if (indexOfAtSign >= 0)
                {
                    serviceControlLogicalQueue = serviceControlLogicalQueue[..indexOfAtSign];
                }

                var routing = new RoutingSettings(configuration.GetSettings());
                routing.RouteToEndpoint(typeof(RegisterNewEndpoint), serviceControlLogicalQueue);
                routing.RouteToEndpoint(typeof(MarkMessageFailureResolvedByRetry), serviceControlLogicalQueue);

                configuration.AddCustomCheck<AuditIngestionCustomCheck>();
                configuration.AddCustomCheck<FailedAuditImportCustomCheck>();

                // SC.Audit runs its custom checks (AuditIngestionCustomCheck, FailedAuditImportCustomCheck)
                // via the custom check mechanism, not the DI-based InternalCustomChecksHostedService used by the primary instance. The results are
                // forwarded as ReportCustomCheckResult messages to the primary instance, which is// the sole owner of the ICustomChecksDataStore persistence.
                // The primary's ReportCustomCheckResultHandler receives these and stores them through CustomCheckResultProcessor — the same path used for custom checks reported by
                // any monitored endpoint in the ecosystem.
                configuration.ReportCustomChecksTo(
                    transportCustomization.ToTransportQualifiedQueueName(settings.ServiceControlQueueAddress),
                    TimeSpan.FromMinutes(1) // Prevent clock skew issues, overrides calculated TTL due to some custom check using short reporting intervals (i.e. 5s results in 20s TTL)
                );
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