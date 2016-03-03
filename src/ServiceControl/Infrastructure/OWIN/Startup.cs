namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition.Hosting;
    using System.IO;
    using Autofac;
    using global::Nancy.Owin;
    using Microsoft.AspNet.SignalR;
    using Microsoft.AspNet.SignalR.Json;
    using NServiceBus.Logging;
    using Owin;
    using Particular.ServiceControl.Licensing;
    using Raven.Database.Config;
    using Raven.Database.Server;
    using ServiceBus.Management.Infrastructure.Nancy;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.OWIN;
    using ServiceControl.Infrastructure.SignalR;
    using JsonNetSerializer = Microsoft.AspNet.SignalR.Json.JsonNetSerializer;

    public class Startup
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(Startup));

        private IContainer container;


        public Startup(IContainer container)
        {
            this.container = container;
        }

        public void Configuration(IAppBuilder app)
        {
            app.UseErrorPage();

            app.Map("/api", b =>
            {
                b.Use<LogApiCalls>();
                ConfigureSignalR(b);
                b.UseNancy(new NancyOptions
                {
                    Bootstrapper = new NServiceBusContainerBootstrapper(container)
                });
            });

            ConfigureRavenDB(app);
        }

        static string ReadLicense()
        {
            using (var resourceStream = typeof(Startup).Assembly.GetManifestResourceStream("ServiceControl.Infrastructure.RavenDB.RavenLicense.xml"))
            {
                using (var reader = new StreamReader(resourceStream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public void ConfigureRavenDB(IAppBuilder builder)
        {
            builder.Map("/storage", app =>
            {
                string virtualDirectory = "storage";
                if (!String.IsNullOrEmpty(Settings.VirtualDirectory))
                {
                    virtualDirectory = Settings.VirtualDirectory + "/" + virtualDirectory;
                }

                var configuration = new RavenConfiguration
                {
                    VirtualDirectory = virtualDirectory,
                    DataDirectory = Path.Combine(Settings.DbPath, "Databases", "System"),
                    CompiledIndexCacheDirectory = Path.Combine(Settings.DbPath, "CompiledIndexes"),
                    CountersDataDirectory = Path.Combine(Settings.DbPath, "Data", "Counters"),
                    WebDir = Path.Combine(Settings.DbPath, "Raven", "WebUI"),
                    PluginsDirectory = Path.Combine(Settings.DbPath, "Plugins"),
                    AssembliesDirectory = Path.Combine(Settings.DbPath, "Assemblies"),
                };

                var localRavenLicense = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RavenLicense.xml");
                if (File.Exists(localRavenLicense))
                {
                    Logger.InfoFormat("Loading RavenDB license found from {0}", localRavenLicense);
                    configuration.Settings["Raven/License"] = NonLockingFileReader.ReadAllTextWithoutLocking(localRavenLicense);
                }
                else
                {
                    Logger.InfoFormat("Loading Embedded RavenDB license");
                    configuration.Settings["Raven/License"] = ReadLicense();
                }

                configuration.FileSystem.DataDirectory = Path.Combine(Settings.DbPath, "FileSystems");
                configuration.FileSystem.IndexStoragePath = Path.Combine(Settings.DbPath, "FileSystems", "Indexes");
                configuration.Catalog.Catalogs.Add(new AssemblyCatalog(GetType().Assembly));

                app.UseRavenDB(new RavenDBOptions(configuration));
            });
        }

        private void ConfigureSignalR(IAppBuilder app)
        {
            var resolver = new AutofacDependencyResolver(container);

            app.MapConnection<MessageStreamerConnection>("/messagestream",
                new ConnectionConfiguration
                {
                    EnableCrossDomain = true,
                    Resolver = resolver
                });

            GlobalHost.DependencyResolver = resolver;

            var jsonSerializer = new JsonNetSerializer(SerializationSettingsFactoryForSignalR.CreateDefault());
            GlobalHost.DependencyResolver.Register(typeof(IJsonSerializer), () => jsonSerializer);

            GlobalEventHandler.SignalrIsReady = true;
        }
    }

    class AutofacDependencyResolver : DefaultDependencyResolver
    {
        private IContainer container;

        public AutofacDependencyResolver(IContainer container)
        {
            this.container = container;
        }

        public override object GetService(Type serviceType)
        {
            object service;
            if (container.TryResolve(serviceType, out service))
            {
                return service;
            }
            return base.GetService(serviceType);
        }

        public override IEnumerable<object> GetServices(Type serviceType)
        {
            object services;
            if (container.TryResolve(typeof(IEnumerable<>).MakeGenericType(serviceType), out services))
            {
                return (IEnumerable<object>) services;
            }

            return base.GetServices(serviceType);
        }
    }
}