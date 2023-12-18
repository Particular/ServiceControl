namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http.Headers;
    using System.Reflection;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Infrastructure.WebApi;

    // TODO this is only used by the ServiceControlComponentRunner
    class Startup
    {
        public Startup(IServiceProvider serviceProvider, List<Assembly> assemblies)
        {
            this.serviceProvider = serviceProvider;
            this.assemblies = assemblies;
        }

        public void Configuration(IAppBuilder app, Assembly additionalAssembly = null)
        {
            if (additionalAssembly != null)
            {
                assemblies.Add(additionalAssembly);
            }

            app.Map("/api", b =>
            {
                var config = new HttpConfiguration();
                config.MapHttpAttributeRoutes();

                config.Services.Replace(typeof(IAssembliesResolver), new OnlyExecutingAssemblyResolver(additionalAssembly));

                var jsonMediaTypeFormatter = config.Formatters.JsonFormatter;
                jsonMediaTypeFormatter.SerializerSettings = JsonNetSerializerSettings.CreateDefault();
                jsonMediaTypeFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/vnd.particular.1+json"));
                config.Formatters.Remove(config.Formatters.XmlFormatter);

                config.DependencyResolver = new ExternallyOwnedContainerDependencyResolver(serviceProvider);

                config.MessageHandlers.Add(new XParticularVersionHttpHandler());
                config.MessageHandlers.Add(new CompressionEncodingHttpHandler());
                config.MessageHandlers.Add(new CachingHttpHandler());
                config.MessageHandlers.Add(new NotModifiedStatusHttpHandler());

                b.UseWebApi(config);
            });
        }

        readonly IServiceProvider serviceProvider;
        readonly List<Assembly> assemblies;
    }

    class ExternallyOwnedContainerDependencyResolver : System.Web.Http.Dependencies.IDependencyResolver
    {
        IServiceProvider impl;

        public ExternallyOwnedContainerDependencyResolver(IServiceProvider serviceProvider)
        {
            impl = serviceProvider;
        }

        public void Dispose()
        {
            //NOOP We don't dispose the underlying container
        }

        public object GetService(Type serviceType) => impl.GetService(serviceType);

        public IEnumerable<object> GetServices(Type serviceType) => impl.GetServices(serviceType);

        public IDependencyScope BeginScope() => new ServiceProviderScope(impl.CreateScope());

        class ServiceProviderScope : IDependencyScope
        {
            readonly IServiceScope scope;

            public ServiceProviderScope(IServiceScope scope)
            {
                this.scope = scope;
            }

            public void Dispose() => scope.Dispose();

            public object GetService(Type serviceType) =>
                scope.ServiceProvider.GetService(serviceType);

            public IEnumerable<object> GetServices(Type serviceType) =>
                scope.ServiceProvider.GetServices(serviceType);
        }
    }

    class OnlyExecutingAssemblyResolver : DefaultAssembliesResolver
    {
        public OnlyExecutingAssemblyResolver(Assembly additionalAssembly)
        {
            this.additionalAssembly = additionalAssembly;
        }

        public override ICollection<Assembly> GetAssemblies()
        {
            if (additionalAssembly != null)
            {
                return new[] { Assembly.GetExecutingAssembly(), additionalAssembly };
            }

            return new[] { Assembly.GetExecutingAssembly() };
        }

        readonly Assembly additionalAssembly;
    }

    class MicrosoftDependencyResolver : DefaultDependencyResolver
    {
        public MicrosoftDependencyResolver(IServiceProvider serviceProvider) => this.serviceProvider = serviceProvider;

        public override object GetService(Type serviceType)
        {
            return serviceProvider.GetService(serviceType) ??
                   base.GetService(serviceType);
        }

        public override IEnumerable<object> GetServices(Type serviceType)
        {
            return serviceProvider.GetServices(serviceType) ??
                   base.GetServices(serviceType);
        }

        readonly IServiceProvider serviceProvider;
    }
}