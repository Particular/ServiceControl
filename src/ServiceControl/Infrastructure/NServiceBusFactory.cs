namespace ServiceBus.Management.Infrastructure
{
    using System;
    using System.Diagnostics;
    using System.ServiceProcess;
    using Autofac;
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Raven.Client;
    using ServiceControl.Infrastructure;

    public static class NServiceBusFactory
    {
        public static IStartableBus Create(Settings.Settings settings, IContainer container, ServiceBase host, IDocumentStore documentStore, BusConfiguration configuration)
        {
            if (configuration == null)
            {
                configuration = new BusConfiguration();
                configuration.AssembliesToScan(AllAssemblies.Except("ServiceControl.Plugin"));
            }

            // HACK: Yes I know, I am hacking it to pass it to RavenBootstrapper!
            configuration.GetSettings().Set("ServiceControl.EmbeddableDocumentStore", documentStore);
            configuration.GetSettings().Set("ServiceControl.Settings", settings);

            // Disable Auditing for the service control endpoint
            configuration.DisableFeature<Audit>();
            configuration.DisableFeature<AutoSubscribe>();
            configuration.DisableFeature<SecondLevelRetries>();
            configuration.DisableFeature<TimeoutManager>();
            configuration.DisableFeature<Outbox>();

            // Disable Callback queue creation for SQL Transport so it doesn't create the ServiceControl.MachineName queue.
            configuration.ScaleOut().UseSingleBrokerQueue();

            configuration.UseSerialization<JsonSerializer>();

            configuration.Transactions()
                .DisableDistributedTransactions()
                .DoNotWrapHandlersExecutionInATransactionScope();

            configuration.ScaleOut().UseSingleBrokerQueue();

            var transportType = DetermineTransportType(settings);

            configuration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));
            configuration.EndpointName(settings.ServiceName);
            configuration.UseContainer<AutofacBuilder>(c => c.ExistingLifetimeScope(container));
            var transport = configuration.UseTransport(transportType);
            if (settings.TransportConnectionString != null)
            {
                transport.ConnectionString(settings.TransportConnectionString);
            }
            configuration.DefineCriticalErrorAction((s, exception) =>
            {
                host?.Stop();
            });

            if (Environment.UserInteractive && Debugger.IsAttached)
            {
                configuration.EnableInstallers();
            }

            return Bus.Create(configuration);
        }

        public static IBus CreateAndStart(Settings.Settings settings, IContainer container, ServiceBase host, IDocumentStore documentStore, BusConfiguration configuration)
        {
            var bus = Create(settings, container, host, documentStore, configuration);

            container.Resolve<SubscribeToOwnEvents>().Run();

            return bus.Start();
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