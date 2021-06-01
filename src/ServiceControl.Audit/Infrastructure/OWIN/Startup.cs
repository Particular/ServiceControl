namespace ServiceControl.Audit.Infrastructure.OWIN
{
    using System.Collections.Generic;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Web.Http;
    using System.Web.Http.Dispatcher;
    using Autofac;
    using Autofac.Integration.WebApi;
    using Owin;
    using WebApi;

    class Startup
    {
        public Startup(ILifetimeScope container)
        {
            this.container = container;
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

                config.DependencyResolver = new AutofacWebApiDependencyResolver(container);
                config.Services.Replace(typeof(IAssembliesResolver), new OnlyExecutingAssemblyResolver(additionalAssembly));
                config.MapHttpAttributeRoutes();

                map.UseCors(Cors.AuditCorsOptions);

                config.MessageHandlers.Add(new XParticularVersionHttpHandler());
                config.MessageHandlers.Add(new CompressionEncodingHttpHandler());
                config.MessageHandlers.Add(new CachingHttpHandler());
                config.MessageHandlers.Add(new NotModifiedStatusHttpHandler());

                map.UseWebApi(config);
            });
        }

        ILifetimeScope container;
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
}