namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Web.Http;
    using System.Web.Http.Controllers;
    using System.Web.Http.Dependencies;
    using System.Web.Http.Dispatcher;
    using Microsoft.AspNet.SignalR;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Owin.Cors;
    using Newtonsoft.Json;
    using Owin;
    using ServiceControl.Infrastructure.OWIN;
    using ServiceControl.Infrastructure.SignalR;
    using ServiceControl.Infrastructure.WebApi;

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
                b.Use<BodyUrlRouteFix>();
                b.Use<LogApiCalls>();

                b.UseCors(Cors.AuditCorsOptions);

                ConfigureSignalR(b);

                var config = new HttpConfiguration();
                config.MapHttpAttributeRoutes();

                config.Services.Replace(typeof(IAssembliesResolver), new OnlyExecutingAssemblyResolver(additionalAssembly));
                config.Services.Replace(typeof(IHttpControllerTypeResolver), new InternalControllerTypeResolver());

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

        void ConfigureSignalR(IAppBuilder app)
        {
            var resolver = new MicrosoftDependencyResolver(serviceProvider);

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

    /// <summary>
    /// Replaces the <see cref="DefaultHttpControllerTypeResolver"/> with a similar implementation that allows non-public controllers.
    /// </summary>
    class InternalControllerTypeResolver : IHttpControllerTypeResolver
    {
        public ICollection<Type> GetControllerTypes(IAssembliesResolver assembliesResolver)
        {
            var controllerTypes = new List<Type>();
            foreach (Assembly assembly in assembliesResolver.GetAssemblies())
            {
                if (assembly != null && !assembly.IsDynamic)
                {
                    Type[] source;
                    try
                    {
                        source = assembly.GetTypes();

                    }
                    catch
                    {
                        continue;
                    }

                    controllerTypes.AddRange(source.Where(t =>
                        t != null &&
                        t.IsClass &&
                        !t.IsAbstract &&
                        typeof(IHttpController).IsAssignableFrom(t) &&
                        HasValidControllerName(t)));
                }
            }

            return controllerTypes;
        }

        internal static bool HasValidControllerName(Type controllerType)
        {
            string controllerSuffix = DefaultHttpControllerSelector.ControllerSuffix;
            return controllerType.Name.Length > controllerSuffix.Length &&
                   controllerType.Name.EndsWith(controllerSuffix, StringComparison.OrdinalIgnoreCase);
        }
    }
}