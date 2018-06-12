namespace ServiceBus.Management.Infrastructure
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Autofac;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.DomainEvents;

    public static class NServiceBusFactory
    {
        public static Task<IStartableEndpoint> Create(Settings.Settings settings, IContainer container, Action onCriticalError, IDocumentStore documentStore, EndpointConfiguration configuration, bool isRunningAcceptanceTests)
        {
            if (configuration == null)
            {
                configuration = new EndpointConfiguration(settings.ServiceName);
                var assemblyScanner = configuration.AssemblyScanner();
                assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            }

            // HACK: Yes I know, I am hacking it to pass it to RavenBootstrapper!
            configuration.GetSettings().Set("ServiceControl.EmbeddableDocumentStore", documentStore);
            configuration.GetSettings().Set("ServiceControl.Settings", settings);
            configuration.GetSettings().Set("ServiceControl.MarkerFileService", new MarkerFileService(new LoggingSettings(settings.ServiceName).LogPath));

            // Disable Auditing for the service control endpoint
            configuration.DisableFeature<Audit>();
            configuration.DisableFeature<AutoSubscribe>();
            configuration.DisableFeature<TimeoutManager>();
            configuration.DisableFeature<Outbox>();

            configuration.Recoverability().Delayed(c => c.NumberOfRetries(0));

            configuration.UseSerialization<NewtonsoftSerializer>();

            var transportType = DetermineTransportType(settings);

            configuration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));

            if (!isRunningAcceptanceTests)
            {
                configuration.ReportCustomChecksTo(settings.ServiceName);
            }

            configuration.UseContainer<AutofacBuilder>(c => c.ExistingLifetimeScope(container));
            var transport = configuration.UseTransport(transportType);
            transport.Transactions(TransportTransactionMode.ReceiveOnly);
            
            if (settings.TransportConnectionString != null)
            {
                transport.ConnectionString(settings.TransportConnectionString);
            }
            configuration.DefineCriticalErrorAction(criticalErrorContext =>
            {
                onCriticalError();
                return Task.FromResult(0);
            });

            if (Environment.UserInteractive && Debugger.IsAttached)
            {
                configuration.EnableInstallers();
            }

            return Endpoint.Create(configuration);
        }

        public static async Task<BusInstance> CreateAndStart(Settings.Settings settings, IContainer container, Action onCriticalError, IDocumentStore documentStore, EndpointConfiguration configuration, bool isRunningAcceptanceTests)
        {
            var bus = await Create(settings, container, onCriticalError, documentStore, configuration, isRunningAcceptanceTests)
                .ConfigureAwait(false);

            container.Resolve<SubscribeToOwnEvents>().Run();
            var domainEvents = container.Resolve<IDomainEvents>();

            var startedBus = await bus.Start().ConfigureAwait(false);
            return new BusInstance(startedBus, domainEvents);
        }

        static Type DetermineTransportType(Settings.Settings settings)
        {
            var logger = LogManager.GetLogger(typeof(NServiceBusFactory));
            var transportType = Type.GetType(settings.TransportType);
            if (transportType != null)
            {
                return transportType;
            }
            var errorMsg = $"Configuration of transport Failed. Could not resolve type '{settings.TransportType}' from Setting 'TransportType'. Ensure the assembly is present and that type is correctly defined in settings";
            logger.Error(errorMsg);
            throw new Exception(errorMsg);
        }

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null && t.Namespace.StartsWith("ServiceControl.Contracts");
        }
    }
}