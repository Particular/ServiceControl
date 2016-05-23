namespace Particular.ServiceControl
{
    using System;
    using Microsoft.Owin.Hosting;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceBus.Management.Infrastructure.Settings;

    public class MaintenanceBootstrapper
    {
        public void Run()
        {
            var startup = new Startup(null);
            using (WebApp.Start(new StartOptions(Settings.RootUrl), startup.ConfigureRavenDB))
            {
                Console.Out.WriteLine("RavenDB is now accepting requests on {0}", Settings.StorageUrl);
                Console.Out.WriteLine("RavenDB Maintenance Mode - Press Enter to exit");
                Console.ReadLine();
            }
        }
    }
}