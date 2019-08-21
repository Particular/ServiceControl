using NServiceBus.Features;
using System.Threading.Tasks;
using Nancy.Hosting.Self;
using NServiceBus;

namespace ServiceControl.Monitoring.Http
{
    using System;
    using Autofac;
    using Nancy.Bootstrapper;
    using Nancy.Bootstrappers.Autofac;

    class HttpEndpoint : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings.Get<Settings>();
            var hostname = settings.HttpHostName;
            var port = settings.HttpPort;

            if (!int.TryParse(port, out _))
            {
                throw new Exception($"Http endpoint port is wrongly formatted. It should be a valid integer but it is '{port}'.");
            }

            if (string.IsNullOrEmpty(hostname))
            {
                throw new Exception("No host name provided.");
            }

            var host = new Uri($"http://{hostname}:{port}");

            context.RegisterStartupTask(builder => BuildTask(builder.Build<ILifetimeScope>(), host));
        }

        FeatureStartupTask BuildTask(ILifetimeScope container, Uri host)
        {
            return new NancyTask(container, host);
        }

        class NancyTask : FeatureStartupTask
        {
            NancyHost metricsEndpoint;

            public NancyTask(ILifetimeScope container, Uri host)
            {
                var hostConfiguration = new HostConfiguration { RewriteLocalhost = false };

                metricsEndpoint = new NancyHost(host, new Bootstrapper(container), hostConfiguration);
            }

            protected override Task OnStart(IMessageSession session)
            {
                metricsEndpoint?.Start();
                return Task.FromResult(0);
            }

            protected override Task OnStop(IMessageSession session)
            {
                metricsEndpoint?.Dispose();
                return Task.FromResult(0);
            }
        }

        class Bootstrapper : AutofacNancyBootstrapper
        {
            readonly ILifetimeScope lifetimeScope;

            protected override void ApplicationStartup(ILifetimeScope container, IPipelines pipelines)
            {
                Nancy.Json.JsonSettings.MaxJsonLength = int.MaxValue;

                base.ApplicationStartup(container, pipelines);
            }

            public Bootstrapper(ILifetimeScope lifetimeScope)
            {
                this.lifetimeScope = lifetimeScope;
            }

            protected override ILifetimeScope GetApplicationContainer()
            {
                return lifetimeScope;
            }            
        }
    }
}