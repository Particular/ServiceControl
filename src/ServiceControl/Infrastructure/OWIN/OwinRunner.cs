﻿namespace ServiceBus.Management.Infrastructure.OWIN
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
            try
            {
                webApp = WebApp.Start<Startup>(Settings.ApiUrl);
                Logger.InfoFormat("Api is now accepting requests on {0}", Settings.ApiUrl);
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine(ex);
            }
        }

        public void Stop()
        {
            if (webApp == null)
            {
                return;
            }

            webApp.Dispose();
            Logger.InfoFormat("Api is now stopped");
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(OwinRunner));
        IDisposable webApp;
    }
}