namespace ServiceBus.Management.Infrastructure
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Autofac;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using Raven.Client.Embedded;
    using ServiceControl.CustomChecks;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Operations;
    using ServiceControl.Transports;
    using Settings;

    static class NServiceBusFactory
    {
        public static Task<IStartableEndpoint> Create(Settings.Settings settings, TransportCustomization transportCustomization, TransportSettings transportSettings, LoggingSettings loggingSettings, IContainer container, EmbeddableDocumentStore documentStore, EndpointConfiguration configuration, bool isRunningAcceptanceTests)
        {
            var endpointName = settings.ServiceName;
            if (configuration == null)
            {
                configuration = new EndpointConfiguration(endpointName);
                var assemblyScanner = configuration.AssemblyScanner();
                assemblyScanner.ExcludeAssemblies("ServiceControl.Plugin");
            }

            // HACK: Yes I know, I am hacking it to pass it to RavenBootstrapper!
            configuration.GetSettings().Set(documentStore);
            configuration.GetSettings().Set("ServiceControl.Settings", settings);

            MapSettings(transportSettings, settings);

            transportCustomization.CustomizeEndpoint(configuration, transportSettings);

            configuration.GetSettings().Set(loggingSettings);

            // Disable Auditing for the service control endpoint
            configuration.DisableFeature<Audit>();
            configuration.DisableFeature<AutoSubscribe>();
            configuration.DisableFeature<TimeoutManager>();
            configuration.DisableFeature<Outbox>();

            var recoverability = configuration.Recoverability();
            recoverability.Immediate(c => c.NumberOfRetries(3));
            recoverability.Delayed(c => c.NumberOfRetries(0));
            configuration.SendFailedMessagesTo($"{endpointName}.Errors");

            configuration.UseSerialization<NewtonsoftSerializer>();

            configuration.LimitMessageProcessingConcurrencyTo(settings.MaximumConcurrencyLevel);

            configuration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));

            if (!isRunningAcceptanceTests)
            {
                configuration.EnableFeature<InternalCustomChecks>();
            }

#pragma warning disable CS0618 // Type or member is obsolete
            configuration.UseContainer<AutofacBuilder>(c => c.ExistingLifetimeScope(container));
#pragma warning restore CS0618 // Type or member is obsolete

            configuration.DefineCriticalErrorAction(CriticalErrorCustomCheck.OnCriticalError);

            if (Environment.UserInteractive && Debugger.IsAttached)
            {
                configuration.EnableInstallers();
            }

            return Endpoint.Create(configuration);
        }

        private static void MapSettings(TransportSettings transportSettings, Settings.Settings settings)
        {
            transportSettings.EndpointName = settings.ServiceName;
            transportSettings.ConnectionString = settings.TransportConnectionString;
            transportSettings.MaxConcurrency = settings.MaximumConcurrencyLevel;
        }

        public static async Task<BusInstance> CreateAndStart(Settings.Settings settings, TransportCustomization transportCustomization, TransportSettings transportSettings, LoggingSettings loggingSettings, IContainer container, EmbeddableDocumentStore documentStore, EndpointConfiguration configuration, bool isRunningAcceptanceTests)
        {
            var startableEndpoint = await Create(settings, transportCustomization, transportSettings, loggingSettings, container, documentStore, configuration, isRunningAcceptanceTests)
                .ConfigureAwait(false);

            var domainEvents = container.Resolve<IDomainEvents>();
            var errorIngestion = container.Resolve<ErrorIngestionComponent>();

            var endpointInstance = await startableEndpoint.Start().ConfigureAwait(false);

            var builder = new ContainerBuilder();

            builder.RegisterInstance(endpointInstance).As<IMessageSession>();

            builder.Update(container.ComponentRegistry);

            return new BusInstance(endpointInstance, domainEvents, errorIngestion);
        }

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null
                   && t.Namespace.StartsWith("ServiceControl.Contracts")
                   && t.Assembly.GetName().Name == "ServiceControl.Contracts";
        }
    }
}