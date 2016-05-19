namespace Particular.ServiceControl
{
    using System;
    using System.Diagnostics;
    using System.ServiceProcess;
    using Autofac;
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;

    public abstract class BootstrapperBase
    {
        protected IContainer container;
        protected EmbeddableDocumentStore documentStore = new EmbeddableDocumentStore();

        protected ServiceBase host;
        protected ILog logger;

        protected IStartableBus ConfigureNServiceBus(BusConfiguration configuration)
        {
            if (configuration == null)
            {
                configuration = new BusConfiguration();
                configuration.AssembliesToScan(AllAssemblies.Except("ServiceControl.Plugin"));
            }

            // HACK: Yes I know, I am hacking it to pass it to RavenBootstrapper!
            configuration.GetSettings().Set("ServiceControl.EmbeddableDocumentStore", documentStore);

            // Disable Auditing for the service control endpoint
            configuration.DisableFeature<Audit>();
            configuration.DisableFeature<AutoSubscribe>();
            configuration.DisableFeature<SecondLevelRetries>();
            configuration.DisableFeature<TimeoutManager>();

            configuration.UseSerialization<JsonSerializer>();

            configuration.Transactions()
                .DisableDistributedTransactions()
                .DoNotWrapHandlersExecutionInATransactionScope();

            configuration.ScaleOut().UseSingleBrokerQueue();

            var transportType = DetermineTransportType();

            configuration.Conventions().DefiningEventsAs(t => typeof(IEvent).IsAssignableFrom(t) || IsExternalContract(t));
            configuration.EndpointName(Settings.ServiceName);
            configuration.UseContainer<AutofacBuilder>(c => c.ExistingLifetimeScope(container));
            configuration.UseTransport(transportType);
            configuration.DefineCriticalErrorAction((s, exception) => { host?.Stop(); });

            if (Environment.UserInteractive && Debugger.IsAttached)
            {
                configuration.EnableInstallers();
            }

            return Bus.Create(configuration);
        }

        Type DetermineTransportType()
        {
            var transportType = Type.GetType(Settings.TransportType);
            if (transportType != null)
            {
                return transportType;
            }
            var errorMsg = $"Configuration of transport Failed. Could not resolve type '{Settings.TransportType}' from Setting 'TransportType'. Ensure the assembly is present and that type is correctly defined in settings";
            logger.Error(errorMsg);
            throw new Exception(errorMsg);
        }

        private static bool IsExternalContract(Type t)
        {
            return t.Namespace != null && t.Namespace.StartsWith("ServiceControl.Contracts");
        }
    }
}