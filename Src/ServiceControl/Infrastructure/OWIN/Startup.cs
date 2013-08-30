namespace ServiceBus.Management.Infrastructure.OWIN
{
    using Microsoft.AspNet.SignalR;
    using Nancy;
    using Owin;
    using ServiceControl.Infrastructure.SignalR;

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseErrorPage();
            
            app.MapConnection<MessageStreamerConnection>("/messagestream",
                new ConnectionConfiguration
                {
                    EnableCrossDomain = true,
                });
            app.UseNancy(new NServiceBusContainerBootstrapper());
        }
    }
}