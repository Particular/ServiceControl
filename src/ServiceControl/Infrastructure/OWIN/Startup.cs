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
            
            app.MapConnection<MessageStreamerConnection>("/messagestream",
                new ConnectionConfiguration
                {
                    EnableCrossDomain = true,
                    Resolver = new Foo()
                });

            app.UseNancy(new NancyOptions { Bootstrapper = new NServiceBusContainerBootstrapper() });
        }
    }

    class Foo : IDependencyResolver
    {
        public void Dispose()
        {
           
        }

        public object GetService(Type serviceType)
        {
            return Bootstrapper.Container.Resolve(serviceType);
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            return (IEnumerable<object>)Bootstrapper.Container.Resolve(typeof(IEnumerable<>).MakeGenericType(serviceType));
        }

        public void Register(Type serviceType, Func<object> activator)
        {
            throw new Exception();
        }

        public void Register(Type serviceType, IEnumerable<Func<object>> activators)
        {
            throw new Exception();
        }
    }
}