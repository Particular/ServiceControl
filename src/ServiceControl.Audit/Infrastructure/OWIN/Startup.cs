namespace ServiceControl.Audit.Infrastructure.OWIN
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
    using Microsoft.Extensions.DependencyInjection;
    using Owin;
    using WebApi;

    class Startup
    {
        public Startup(IServiceProvider serviceProvider)
        {
            container = serviceProvider;
        }

        public void Configuration(IAppBuilder appBuilder, Assembly additionalAssembly = null)
        {
            appBuilder.Map("/api", map =>
            {
                map.Use<BodyUrlRouteFix>();
                var config = new HttpConfiguration();

                var jsonMediaTypeFormatter = config.Formatters.JsonFormatter;
                jsonMediaTypeFormatter.SerializerSettings = JsonNetSerializerSettings.CreateDefault();
                jsonMediaTypeFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/vnd.particular.1+json"));
                config.Formatters.Remove(config.Formatters.XmlFormatter);

                config.DependencyResolver = new ServiceProviderDependencyResolver(container);
                config.Services.Replace(typeof(IAssembliesResolver), new OnlyExecutingAssemblyResolver(additionalAssembly));
                config.Services.Replace(typeof(IHttpControllerTypeResolver), new InternalControllerTypeResolver());
                config.MapHttpAttributeRoutes();
                map.UseCors(Cors.AuditCorsOptions);

                config.MessageHandlers.Add(new XParticularVersionHttpHandler());
                config.MessageHandlers.Add(new CompressionEncodingHttpHandler());
                config.MessageHandlers.Add(new CachingHttpHandler());
                config.MessageHandlers.Add(new NotModifiedStatusHttpHandler());

                map.UseWebApi(config);
            });
        }

        IServiceProvider container;
    }

    class ServiceProviderDependencyResolver : IDependencyResolver
    {
        IServiceProvider serviceProvider;

        public ServiceProviderDependencyResolver(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public void Dispose()
        {
        }

        public object GetService(Type serviceType) => serviceProvider.GetService(serviceType);

        public IEnumerable<object> GetServices(Type serviceType) => serviceProvider.GetServices(serviceType);

        public IDependencyScope BeginScope() => new ServiceProviderScope(serviceProvider.CreateScope());

        class ServiceProviderScope : IDependencyScope
        {
            readonly IServiceScope scope;

            public ServiceProviderScope(IServiceScope scope)
            {
                this.scope = scope;
            }

            public void Dispose() => scope.Dispose();

            public object GetService(Type serviceType) => scope.ServiceProvider.GetService(serviceType);

            public IEnumerable<object> GetServices(Type serviceType) => scope.ServiceProvider.GetServices(serviceType);
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

                    controllerTypes.AddRange(source.Where(t => t != null && t.IsClass && !t.IsAbstract && typeof(IHttpController).IsAssignableFrom(t) && HasValidControllerName(t)));
                }
            }
            return controllerTypes;
        }

        internal static bool HasValidControllerName(Type controllerType)
        {
            string controllerSuffix = DefaultHttpControllerSelector.ControllerSuffix;
            return controllerType.Name.Length > controllerSuffix.Length && controllerType.Name.EndsWith(controllerSuffix, StringComparison.OrdinalIgnoreCase);
        }
    }
}