namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System.Web.Http;
    using Autofac;
    using Autofac.Integration.WebApi;
    using Owin;
    using ServiceControl.Monitoring.Infrastructure.OWIN;
    using ServiceControl.Monitoring.Infrastructure.WebApi;

    public class Startup
    {
        public Startup(IContainer container)
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
            config.DependencyResolver = new AutofacWebApiDependencyResolver(container);

            config.MessageHandlers.Add(new CachingHttpHandler());
            app.UseWebApi(config);
        }

        readonly IContainer container;
    }
}