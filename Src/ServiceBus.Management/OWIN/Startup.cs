namespace ServiceBus.Management.OWIN
{
    using Autofac;
    using Autofac.Integration.SignalR;
    using Microsoft.AspNet.SignalR;
    using Nancy;
    using Owin;
    using SignalR;

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            RegisterPersistentConnections();

            app.UseErrorPage();
            
            app.MapConnection<MessageStreamerConnection>("/messagestream",
                new ConnectionConfiguration
                {
                    EnableCrossDomain = true,
                    Resolver = new AutofacDependencyResolver(EndpointConfig.Container)
                });
            app.UseNancy(new NServiceBusContainerBootstrapper());
        }

        static void RegisterPersistentConnections()
        {
            var builder = new ContainerBuilder();
            builder.RegisterType<MessageStreamerConnection>()
                .PropertiesAutowired()
                .ExternallyOwned();
            
            builder.Update(EndpointConfig.Container.ComponentRegistry);
        }
    }
}