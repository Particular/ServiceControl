namespace ServiceBus.Management.Infrastructure
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Autofac;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using Raven.Client;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.EndpointControl;
    using ServiceControl.Contracts.MessageFailures;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Operations;
    using ServiceControl.Transports;

    public static class NServiceBusFactory
    {
        public static Task<IStartableEndpoint> Create(Settings.Settings settings, TransportCustomization transportCustomization, LoggingSettings loggingSettings, IContainer container, Action<ICriticalErrorContext> onCriticalError, IDocumentStore documentStore, EndpointConfiguration configuration, bool isRunningAcceptanceTests)
        {
            var endpointName = settings.ServiceName;
            if (configuration == null)
            {
                configuration = new EndpointConfiguration(endpointName);
                var assemblyScanner = configuration.AssemblyScanner();
                assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            }

            // HACK: Yes I know, I am hacking it to pass it to RavenBootstrapper!
            configuration.GetSettings().Set<EmbeddableDocumentStore>(documentStore);
            configuration.GetSettings().Set("ServiceControl.Settings", settings);
            configuration.GetSettings().Set("ServiceControl.RemoteInstances", settings.RemoteInstances.Select(x => x.QueueAddress).ToArray());
            configuration.GetSettings().Set("ServiceControl.RemoteTypesToSubscribeTo", remoteTypesToSubscribeTo);
            configuration.GetSettings().Set("ServiceControl.EndpointName", endpointName);

            transportCustomization.CustomizeEndpoint(configuration, settings.TransportConnectionString);

            configuration.GetSettings().Set("ServiceControl.MarkerFileService", new MarkerFileService(loggingSettings.LogPath));
            configuration.GetSettings().Set<LoggingSettings>(loggingSettings);
            configuration.GetSettings().Set<IDocumentStore>(documentStore);

            // Disable Auditing for the service control endpoint
            configuration.DisableFeature<Audit>();
            configuration.DisableFeature<AutoSubscribe>();
            configuration.DisableFeature<TimeoutManager>();
            configuration.DisableFeature<Outbox>();

            configuration.EnableFeature<SubscriptionFeature>();

            var recoverability = configuration.Recoverability();
            recoverability.Immediate(c => c.NumberOfRetries(3));
            recoverability.Delayed(c => c.NumberOfRetries(0));
            configuration.SendFailedMessagesTo($"{endpointName}.Errors");

            configuration.UseSerialization<NewtonsoftSerializer>();

            configuration.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

            configuration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));

            if (!isRunningAcceptanceTests)
            {
                configuration.ReportCustomChecksTo(endpointName);
            }

            configuration.UseContainer<AutofacBuilder>(c => c.ExistingLifetimeScope(container));

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


        public static async Task<BusInstance> CreateAndStart(Settings.Settings settings, TransportCustomization transportCustomization, LoggingSettings loggingSettings, IContainer container, Action<ICriticalErrorContext> onCriticalError, IDocumentStore documentStore, EndpointConfiguration configuration, bool isRunningAcceptanceTests)
        {
            var startableEndpoint = await Create(settings, transportCustomization, loggingSettings, container, onCriticalError, documentStore, configuration, isRunningAcceptanceTests)
                .ConfigureAwait(false);

            var domainEvents = container.Resolve<IDomainEvents>();
            var importFailedAudits = container.Resolve<ImportFailedAudits>();

            var endpointInstance = await startableEndpoint.Start().ConfigureAwait(false);

            var builder = new ContainerBuilder();

            builder.RegisterInstance(endpointInstance).As<IMessageSession>();

            builder.Update(container.ComponentRegistry);

            return new BusInstance(endpointInstance, domainEvents, importFailedAudits);
        }

        static Type[] remoteTypesToSubscribeTo = {
            typeof(MessageFailureResolvedByRetry),
            typeof(NewEndpointDetected)
        };

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null 
                   && t.Namespace.StartsWith("ServiceControl.Contracts") 
                   && t.Assembly.GetName().Name == "ServiceControl.Contracts";
        }
    }
}