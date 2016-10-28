namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using global::Nancy.Owin;
    using Microsoft.AspNet.SignalR;
    using Nancy;
    using Owin;
    using ServiceControl.Infrastructure.SignalR;
    using Autofac;
    using Metrics;
    using Microsoft.Owin.Cors;
    using Owin.Metrics;
    using ServiceControl.Infrastructure.OWIN;
    using JsonSerializer = Newtonsoft.Json.JsonSerializer;

    public class Startup
    {
        private readonly IContainer container;

        public Startup(IContainer container)
        {
            this.container = container;
        }

        public void Configuration(IAppBuilder app)
        {
            app.Map("/metrics", b =>
            {
                Metric.Config
                    .WithOwin(middleware => b.Use(middleware), config => config
                        .WithMetricsEndpoint(endpointConfig => endpointConfig.MetricsEndpoint(String.Empty)))
                    .WithAllCounters();
            });

            app.Map("/api", b =>
            {
                b.Use<LogApiCalls>();

                ConfigureSignalR(b);

                b.UseNancy(new NancyOptions
                {
                    Bootstrapper = new NServiceBusContainerBootstrapper(container)
                });
            });
        }

        private void ConfigureSignalR(IAppBuilder app)
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