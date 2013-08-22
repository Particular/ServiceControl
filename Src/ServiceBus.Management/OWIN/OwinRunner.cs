namespace ServiceBus.Management.OWIN
{
    using System;
    using Microsoft.Owin.Hosting;
    using NServiceBus;
    using NServiceBus.Logging;

    public class OwinRunner : IWantToRunWhenBusStartsAndStops
    {
        private IDisposable webApp;

        public void Start()
        {
            webApp = WebApp.Start<Startup>(Settings.ApiUrl);
            Logger.InfoFormat("Api is now accepting requests on {0}", Settings.ApiUrl);
        }

        public void Stop()
        {
            webApp.Dispose();
            Logger.InfoFormat("Api is now stopped");
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(OwinRunner));
    }
}