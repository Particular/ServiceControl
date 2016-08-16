namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using System.ServiceProcess;
    using global::Nancy.Owin;
    using Microsoft.AspNet.SignalR;
    using Nancy;
    using Owin;
    using ServiceControl.Infrastructure.SignalR;
    using Autofac;
    using Microsoft.Owin.Cors;
    using NServiceBus;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.OWIN;
    using JsonSerializer = Newtonsoft.Json.JsonSerializer;

    public class Startup
    {
        private readonly IContainer container;
        private readonly ServiceBase host;
        private readonly Settings settings;
        private readonly EmbeddableDocumentStore documentStore;
        private readonly BusConfiguration configuration;
        private readonly ExposeBus exposeBus;

        public Startup(IContainer container, ServiceBase host, Settings settings, EmbeddableDocumentStore documentStore, BusConfiguration configuration, ExposeBus exposeBus)
        {
            this.container = container;
            this.host = host;
            this.settings = settings;
            this.documentStore = documentStore;
            this.configuration = configuration;
            this.exposeBus = exposeBus;
        }

        public void Configuration(IAppBuilder app)
        {
            var signalrIsReady = new SignalrIsReady();

            app.UseNServiceBus(settings, container, host, documentStore, configuration, exposeBus, signalrIsReady);

            if (settings.SetupOnly)
            {
                return;
            }

            app.Map("/api", b =>
            {
                b.Use<LogApiCalls>();

                ConfigureSignalR(b, signalrIsReady);

                b.UseNancy(new NancyOptions
                {
                    Bootstrapper = new NServiceBusContainerBootstrapper(container)
                });
            });
        }

        private void ConfigureSignalR(IAppBuilder app, SignalrIsReady signalrIsReady)
        {
            var resolver = new AutofacDependencyResolver(container);

            app.Map("/messagestream", map =>
            {
                map.UseCors(CorsOptions.AllowAll);
                map.RunSignalR<MessageStreamerConnection>(
                    new ConnectionConfiguration
                    {
                        EnableJSONP = true,
                        Resolver = resolver
                    });
            });

            GlobalHost.DependencyResolver = resolver;

            var jsonSerializer = JsonSerializer.Create(SerializationSettingsFactoryForSignalR.CreateDefault());
            GlobalHost.DependencyResolver.Register(typeof(JsonSerializer), () => jsonSerializer);

            signalrIsReady.Ready = true;
        }
    }

    class AutofacDependencyResolver : DefaultDependencyResolver
    {
        static Type IEnumerableType = typeof(IEnumerable<>);

        private readonly IContainer container;

        public AutofacDependencyResolver(IContainer container)
        {
            this.container = container;
        }

        public override object GetService(Type serviceType)
        {
            object service;
            if (container.TryResolve(serviceType, out service))
            {
                return service;
            }
            return base.GetService(serviceType);
        }

        public override IEnumerable<object> GetServices(Type serviceType)
        {
            object services;
            
            if (container.TryResolve(IEnumerableType.MakeGenericType(serviceType), out services))
            {
                return (IEnumerable<object>) services;
            }

            return base.GetServices(serviceType);
        }
    }
}