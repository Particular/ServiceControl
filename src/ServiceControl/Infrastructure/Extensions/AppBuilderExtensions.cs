namespace ServiceBus.Management.Infrastructure.Extensions
{
    using System;
    using System.Diagnostics;
    using System.ServiceProcess;
    using System.Threading;
    using Autofac;
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Owin;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.SignalR;

    public class ExposeBus
    {
        public Func<IStartableBus> GetBus;
    }

    public static class AppBuilderExtensions
    {
        public const string HostOnAppDisposing = "host.OnAppDisposing";

        public static IAppBuilder UseNServiceBus(this IAppBuilder app, Settings settings, IContainer container, ServiceBase host, EmbeddableDocumentStore documentStore, BusConfiguration configuration, ExposeBus exposeBus, SignalrIsReady signalrIsReady)
        {
            if (configuration == null)
            {
                configuration = new BusConfiguration();
                configuration.AssembliesToScan(AllAssemblies.Except("ServiceControl.Plugin"));
            }

            configuration.RegisterComponents(components => components.RegisterSingleton(signalrIsReady));

            // HACK: Yes I know, I am hacking it to pass it to RavenBootstrapper!
            configuration.GetSettings().Set("ServiceControl.EmbeddableDocumentStore", documentStore);
            configuration.GetSettings().Set("ServiceControl.Settings", settings);

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

            var bus = Bus.Create(configuration);

            if (settings.SetupOnly)
            {
                return app;
            }

            if (exposeBus != null)
            {
                exposeBus.GetBus = () => bus;
            }

            container.Resolve<SubscribeToOwnEvents>().Run();

            if (app.Properties.ContainsKey(HostOnAppDisposing))
            {
                var appDisposing = (CancellationToken)app.Properties[HostOnAppDisposing];
                if (appDisposing != CancellationToken.None)
                {
                    appDisposing.Register(bus.Dispose);
                }
            }

            bus.Start();

            return app;
        }

        static Type DetermineTransportType(Settings settings)
        {
            var Logger = LogManager.GetLogger(typeof(AppBuilderExtensions));
            var transportType = Type.GetType(settings.TransportType);
            if (transportType != null)
            {
                return transportType;
            }
            var errorMsg = $"Configuration of transport Failed. Could not resolve type '{settings.TransportType}' from Setting 'TransportType'. Ensure the assembly is present and that type is correctly defined in settings";
            Logger.Error(errorMsg);
            throw new Exception(errorMsg);
        }

        static bool IsExternalContract(Type t)
        {
            return t.Namespace != null && t.Namespace.StartsWith("ServiceControl.Contracts");
        }
    }
}