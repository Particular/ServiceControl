namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Web.Http;
    using System.Web.Http.Dependencies;
    using System.Web.Http.Dispatcher;
    using Autofac;
    using Autofac.Integration.WebApi;
    using Microsoft.AspNet.SignalR;
    using Microsoft.Owin.Cors;
    using Newtonsoft.Json;
    using Owin;
    using ServiceControl.Infrastructure.OWIN;
    using ServiceControl.Infrastructure.SignalR;
    using ServiceControl.Infrastructure.WebApi;

    class Startup
    {
        public Startup(ILifetimeScope lifetimeScope, List<Assembly> assemblies)
        {
            this.lifetimeScope = lifetimeScope;
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
                b.Use<BodyUrlRouteFix>();
                b.Use<LogApiCalls>();

                b.UseCors(Cors.AuditCorsOptions);

                ConfigureSignalR(b);

                var config = new HttpConfiguration();
                config.Services.Replace(typeof(IAssembliesResolver), new OnlyExecutingAssemblyResolver(assemblies));
                config.MapHttpAttributeRoutes();

                var jsonMediaTypeFormatter = config.Formatters.JsonFormatter;
                jsonMediaTypeFormatter.SerializerSettings = JsonNetSerializerSettings.CreateDefault();
                jsonMediaTypeFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/vnd.particular.1+json"));
                config.Formatters.Remove(config.Formatters.XmlFormatter);

                config.DependencyResolver = new ExternallyOwnedContainerDependencyResolver(new AutofacWebApiDependencyResolver(lifetimeScope));

                config.MessageHandlers.Add(new XParticularVersionHttpHandler());
                config.MessageHandlers.Add(new CompressionEncodingHttpHandler());
                config.MessageHandlers.Add(new CachingHttpHandler());
                config.MessageHandlers.Add(new NotModifiedStatusHttpHandler());

                b.UseWebApi(config);
            });
        }

        void ConfigureSignalR(IAppBuilder app)
        {
            var resolver = new AutofacDependencyResolver(lifetimeScope);

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

        readonly ILifetimeScope lifetimeScope;
        readonly List<Assembly> assemblies;
    }

    class ExternallyOwnedContainerDependencyResolver : System.Web.Http.Dependencies.IDependencyResolver
    {
        System.Web.Http.Dependencies.IDependencyResolver impl;

        public ExternallyOwnedContainerDependencyResolver(System.Web.Http.Dependencies.IDependencyResolver impl)
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

    class OnlyExecutingAssemblyResolver : DefaultAssembliesResolver
    {
        public OnlyExecutingAssemblyResolver(List<Assembly> apiAssemblies)
        {
            this.apiAssemblies = apiAssemblies;
        }

        public override ICollection<Assembly> GetAssemblies()
        {
            return apiAssemblies;
        }

        readonly List<Assembly> apiAssemblies;
    }

    class AutofacDependencyResolver : DefaultDependencyResolver
    {
        public AutofacDependencyResolver(ILifetimeScope lifetimeScope)
        {
            this.lifetimeScope = lifetimeScope;
        }

        public override object GetService(Type serviceType)
        {
            if (lifetimeScope.TryResolve(serviceType, out var service))
            {
                return service;
            }

            return base.GetService(serviceType);
        }

        public override IEnumerable<object> GetServices(Type serviceType)
        {
            if (lifetimeScope.TryResolve(IEnumerableType.MakeGenericType(serviceType), out var services))
            {
                return (IEnumerable<object>)services;
            }

            return base.GetServices(serviceType);
        }

        readonly ILifetimeScope lifetimeScope;
        static Type IEnumerableType = typeof(IEnumerable<>);
    }
}