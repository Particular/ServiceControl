namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using global::Nancy.Owin;
    using Microsoft.AspNet.SignalR;
    using Microsoft.Owin;
    using Nancy;
    using Owin;
    using ServiceControl.Infrastructure.SignalR;

    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseErrorPage();

            app.Use((context, func) =>
            {
                context.Request.PathBase = new PathString("/api");
                context.Request.Path = new PathString(context.Request.Path.Value.Replace("/api", String.Empty));
                return func();
            });
            
            app.MapConnection<MessageStreamerConnection>("/messagestream",
                new ConnectionConfiguration
                {
                    EnableCrossDomain = true,
                });

            app.UseNancy(new NancyOptions { Bootstrapper = new NServiceBusContainerBootstrapper() });
        }
    }
}