namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Web.Http;
    using System.Web.Http.Dispatcher;
    using Autofac;
    using Autofac.Integration.WebApi;
    using Metrics;
    using Microsoft.AspNet.SignalR;
    using Microsoft.Owin.Cors;
    using Microsoft.Owin.StaticFiles;
    using Newtonsoft.Json;
    using Owin;
    using Owin.Metrics;
    using ServiceControl.Infrastructure.OWIN;
    using ServiceControl.Infrastructure.SignalR;
    using ServiceControl.Infrastructure.WebApi;

    class Startup
    {
        public Startup(IContainer container)
        {
            this.container = container;
        }

        public void Configuration(IAppBuilder app, Assembly additionalAssembly = null)
        {
            app.Map("/metrics", b =>
            {
                Metric.Config
                    .WithOwin(middleware => b.Use(middleware), config => config
                        .WithMetricsEndpoint(endpointConfig => endpointConfig.MetricsEndpoint(string.Empty)))
                    .WithAllCounters();
            });

            app.Map("/api", b =>
            {
                b.Use<BodyUrlRouteFix>();
                b.Use<LogApiCalls>();

                b.UseCors(Cors.AuditCorsOptions);

                ConfigureSignalR(b);

                var config = new HttpConfiguration();
                config.Services.Replace(typeof(IAssembliesResolver), new OnlyExecutingAssemblyResolver(additionalAssembly));
                config.MapHttpAttributeRoutes();

                config.Services.Replace(typeof(IAssembliesResolver), new OnlyExecutingAssemblyResolver(additionalAssembly));

                var jsonMediaTypeFormatter = config.Formatters.JsonFormatter;
                jsonMediaTypeFormatter.SerializerSettings = JsonNetSerializerSettings.CreateDefault();
                jsonMediaTypeFormatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/vnd.particular.1+json"));
                config.Formatters.Remove(config.Formatters.XmlFormatter);

                config.DependencyResolver = new AutofacWebApiDependencyResolver(container);

                config.MessageHandlers.Add(new XParticularVersionHttpHandler());
                config.MessageHandlers.Add(new CompressionEncodingHttpHandler());
                config.MessageHandlers.Add(new CachingHttpHandler());
                config.MessageHandlers.Add(new NotModifiedStatusHttpHandler());

                b.UseWebApi(config);
            });

            app.Map("/web", b =>
            {
                var uiFileSystem = new ZipFileSystem(@"C:\Code\Particular\ServicePulse-1.22.0.zip");

                b.UseDefaultFiles(new DefaultFilesOptions
                {
                    FileSystem = uiFileSystem,
                    DefaultFileNames = new List<string>{ "index.html" }
                });

                var options = new StaticFileOptions
                {
                    FileSystem = uiFileSystem,
                    ServeUnknownFileTypes = true
                };

                b.UseStaticFiles(options);
            });

            app.RedirectRootTo("web");
        }

        private void ConfigureSignalR(IAppBuilder app)
        {
            var resolver = new AutofacDependencyResolver(container);

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

        private readonly IContainer container;
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
                return new[] {Assembly.GetExecutingAssembly(), additionalAssembly};
            }

            return new[] {Assembly.GetExecutingAssembly()};
        }

        readonly Assembly additionalAssembly;
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