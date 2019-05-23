namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using Autofac;
    using global::Nancy.Owin;
    using Metrics;
    using Microsoft.AspNet.SignalR;
    using Nancy;
    using Owin;
    using Owin.Metrics;
    using ServiceControl.Infrastructure.OWIN;

    class Startup
    {
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

                b.UseNancy(new NancyOptions
                {
                    Bootstrapper = new NServiceBusContainerBootstrapper(container)
                });
            });
        }

        private readonly IContainer container;
    }

    class AutofacDependencyResolver : DefaultDependencyResolver
    {
        public AutofacDependencyResolver(IContainer container)
        {
            this.container = container;
        }

        public override object GetService(Type serviceType)
        {
            if (container.TryResolve(serviceType, out var service))
            {
                return service;
            }

            return base.GetService(serviceType);
        }

        public override IEnumerable<object> GetServices(Type serviceType)
        {
            if (container.TryResolve(IEnumerableType.MakeGenericType(serviceType), out var services))
            {
                return (IEnumerable<object>)services;
            }

            return base.GetServices(serviceType);
        }

        private readonly IContainer container;
        static Type IEnumerableType = typeof(IEnumerable<>);
    }
}