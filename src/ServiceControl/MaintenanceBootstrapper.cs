namespace Particular.ServiceControl
{
    using System;
    using Microsoft.Owin.Hosting;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceBus.Management.Infrastructure.Settings;

    public class MaintenanceBootstrapper
    {
        public void Run(HostArguments args)
        {
            if (Environment.UserInteractive)
            {
                var startup = new Startup(null);
                using (WebApp.Start(new StartOptions(Settings.RootUrl), startup.ConfigureRavenDB))
                {
                    Console.Out.WriteLine("RavenDB is now accepting requests on {0}", Settings.StorageUrl);
                    Console.Out.WriteLine("RavenDB Maintenance Mode - Press Enter to exit");
                    Console.ReadLine();
                }

                return;
            }

            using (var service = new MaintenanceHost { ServiceName = args.ServiceName })
            {
                service.Run();
            }
        }
    }
}