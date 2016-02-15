namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using global::Nancy.Owin;
    using Microsoft.AspNet.SignalR;
    using Microsoft.Owin;
    using Nancy;
    using Owin;
    using Particular.ServiceControl;
    using ServiceControl.Infrastructure.SignalR;
    using Autofac;
    using Microsoft.AspNet.SignalR.Json;
    using ServiceControl.Infrastructure.OWIN;
    using JsonNetSerializer = Microsoft.AspNet.SignalR.Json.JsonNetSerializer;

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseErrorPage();

            app.Use((context, func) =>
            {
                if (!context.Request.PathBase.HasValue)
                {
                    context.Request.Path = new PathString("/");
                    context.Request.PathBase = new PathString("/api");
                }

                return func();
            });

            app.Use<LogApiCalls>();

            ConfigureSignalR(app);
            app.UseNancy(new NancyOptions { Bootstrapper = new NServiceBusContainerBootstrapper() });
        }

        private static void ConfigureSignalR(IAppBuilder app)
        {
            var resolver = new AutofacDependencyResolver();

            app.MapConnection<MessageStreamerConnection>("/messagestream",
                new ConnectionConfiguration
                {
                    EnableCrossDomain = true,
                    Resolver = resolver
                });

            GlobalHost.DependencyResolver = resolver;

            var jsonSerializer = new JsonNetSerializer(SerializationSettingsFactoryForSignalR.CreateDefault());
            GlobalHost.DependencyResolver.Register(typeof(IJsonSerializer), () => jsonSerializer);
        }
    }

    class AutofacDependencyResolver : DefaultDependencyResolver
    {
        public override object GetService(Type serviceType)
        {
            object service;
            if (Bootstrapper.Container.TryResolve(serviceType, out service))
            {
                return service;
            }
            return base.GetService(serviceType);
        }

        public override IEnumerable<object> GetServices(Type serviceType)
        {
            object services;
            if (Bootstrapper.Container.TryResolve(typeof(IEnumerable<>).MakeGenericType(serviceType), out services))
            {
                return (IEnumerable<object>) services;
            }

            return base.GetServices(serviceType);
        }
    }
}