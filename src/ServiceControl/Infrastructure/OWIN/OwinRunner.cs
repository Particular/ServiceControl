namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using Microsoft.Owin.Hosting;
    using NServiceBus;
    using NServiceBus.Logging;
    using Settings;

    public class OwinRunner : IWantToRunWhenBusStartsAndStops
    {
        public void Start()
        {
            webApp = WebApp.Start<Startup>(Settings.ApiUrl);
            Logger.InfoFormat("Api is now accepting requests on {0}", Settings.ApiUrl);
        }

        public void Stop()
        {
            
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(OwinRunner));
        IDisposable webApp;
    }
}