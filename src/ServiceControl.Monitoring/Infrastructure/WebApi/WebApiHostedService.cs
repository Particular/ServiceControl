namespace ServiceControl.Monitoring.Infrastructure.WebApi
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Owin.Hosting;
    using ServiceBus.Management.Infrastructure.OWIN;

    class WebApiHostedService : IHostedService
    {
        string rootUrl;
        Startup startup;
        IDisposable webApp;

        public WebApiHostedService(string rootUrl, Startup startup)
        {
            this.rootUrl = rootUrl;
            this.startup = startup;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var startOptions = new StartOptions(rootUrl);

            webApp = WebApp.Start(startOptions, b => startup.Configuration(b));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            webApp?.Dispose();

            return Task.CompletedTask;
        }
    }
}