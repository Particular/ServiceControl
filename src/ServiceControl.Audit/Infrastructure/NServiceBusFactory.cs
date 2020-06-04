namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Auditing;
    using Autofac;
    using Contracts.EndpointControl;
    using Contracts.MessageFailures;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using Raven.Client.Embedded;
    using Settings;
    using Transports;

    static class NServiceBusFactory
    {
        public static Task<IStartableEndpoint> Create(Settings.Settings settings, TransportCustomization transportCustomization, TransportSettings transportSettings, LoggingSettings loggingSettings, IContainer container, Action<ICriticalErrorContext> onCriticalError, EmbeddableDocumentStore documentStore, EndpointConfiguration configuration, bool isRunningAcceptanceTests)
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
            configuration.GetSettings().Set(documentStore);
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

#pragma warning disable CS0618 // Type or member is obsolete
            configuration.UseContainer<AutofacBuilder>(c => c.ExistingLifetimeScope(container));
#pragma warning restore CS0618 // Type or member is obsolete

            configuration.DefineCriticalErrorAction(criticalErrorContext =>
            {
                onCriticalError(criticalErrorContext);
                return Task.FromResult(0);
            });

            if (Environment.UserInteractive && Debugger.IsAttached)
            {
                configuration.EnableInstallers();
            }

            return Endpoint.Create(configuration);
        }

        public static async Task<BusInstance> CreateAndStart(Settings.Settings settings, TransportCustomization transportCustomization, TransportSettings transportSettings, LoggingSettings loggingSettings, IContainer container, Action<ICriticalErrorContext> onCriticalError, EmbeddableDocumentStore documentStore, EndpointConfiguration configuration, bool isRunningAcceptanceTests)
        {
            var startableEndpoint = await Create(settings, transportCustomization, transportSettings, loggingSettings, container, onCriticalError, documentStore, configuration, isRunningAcceptanceTests)
                .ConfigureAwait(false);

            var endpointInstance = await startableEndpoint.Start().ConfigureAwait(false);

            var builder = new ContainerBuilder();

            builder.RegisterInstance(endpointInstance).As<IMessageSession>();

            builder.Update(container.ComponentRegistry);

            var importFailedAudits = container.Resolve<ImportFailedAudits>();

            return new BusInstance(endpointInstance, importFailedAudits);
        }

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null
                   && t.Namespace.StartsWith("ServiceControl.Contracts")
                   && t.Assembly.GetName().Name == "ServiceControl.Contracts";
        }
    }
}