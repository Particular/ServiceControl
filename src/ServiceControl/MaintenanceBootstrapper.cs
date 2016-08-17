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
            var settings = new Settings(args.ServiceName);

            if (Environment.UserInteractive)
            {
                var startup = new Startup(null, settings);
                using (WebApp.Start(new StartOptions(settings.RootUrl), builder => startup.ConfigureRavenDB(builder)))
                {
                    Console.Out.WriteLine("RavenDB is now accepting requests on {0}", settings.StorageUrl);
                    Console.Out.WriteLine("RavenDB Maintenance Mode - Press Enter to exit");
                    Console.ReadLine();
                }

                return;
            }

            using (var service = new MaintenanceHost(settings))
            {
                service.Run();
            }
        }
    }
}