namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using System.Web.Http;
    using System.Web.Http.Dependencies;
    using Autofac;
    using Autofac.Integration.WebApi;
    using Owin;
    using ServiceControl.Monitoring.Infrastructure.OWIN;
    using ServiceControl.Monitoring.Infrastructure.WebApi;

    public class Startup
    {
        public Startup(ILifetimeScope container)
        {
            this.container = container;
        }

        public void Configuration(IAppBuilder app)
        {
            app.Use<LogApiCalls>();

            app.UseCors(Cors.GetDefaultCorsOptions());

            var config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();

            var jsonMediaTypeFormatter = config.Formatters.JsonFormatter;
            jsonMediaTypeFormatter.SerializerSettings = JsonNetSerializerSettings.CreateDefault();
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            config.DependencyResolver = new ExternallyOwnedContainerDependencyResolver(new AutofacWebApiDependencyResolver(container));

            config.MessageHandlers.Add(new XParticularVersionHttpHandler());
            config.MessageHandlers.Add(new CachingHttpHandler());
            app.UseWebApi(config);
        }

        readonly ILifetimeScope container;

        class ExternallyOwnedContainerDependencyResolver : IDependencyResolver
        {
            IDependencyResolver impl;

            public ExternallyOwnedContainerDependencyResolver(IDependencyResolver impl)
            {
                this.impl = impl;
            }

            public void Dispose()
            {
                //NOOP We don't dispose the underlying container
            }

            public object GetService(Type serviceType) => impl.GetService(serviceType);

            public IEnumerable<object> GetServices(Type serviceType) => impl.GetServices(serviceType);

            public IDependencyScope BeginScope() => impl.BeginScope();
        }
    }
}