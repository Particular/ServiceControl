namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using Autofac;
    using global::Nancy.Owin;
    using Metrics;
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
}