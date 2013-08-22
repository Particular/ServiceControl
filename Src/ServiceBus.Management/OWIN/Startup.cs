namespace ServiceBus.Management.OWIN
{
    using Nancy;
    using Microsoft.AspNet.SignalR;
    using Owin;
    using SignalR;

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseErrorPage();
            app.MapConnection<EndpointsConnection>("/stream/endpoints", new ConnectionConfiguration { EnableCrossDomain = true /*, Resolver = new AutofacDependencyResolver(EndpointConfig.Container) */});
            app.UseNancy(new NServiceBusContainerBootstrapper());
        }
    }
}
