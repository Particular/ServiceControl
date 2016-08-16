namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.ServiceProcess;
    using global::Nancy.Owin;
    using Microsoft.AspNet.SignalR;
    using Nancy;
    using Owin;
    using ServiceControl.Infrastructure.SignalR;
    using Autofac;
    using Microsoft.Owin.Cors;
    using NServiceBus;
    using NServiceBus.Logging;
    using Particular.ServiceControl.Licensing;
    using Raven.Client.Embedded;
    using Raven.Database.Config;
    using Raven.Database.Server;
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.OWIN;
    using JsonSerializer = Newtonsoft.Json.JsonSerializer;

    public class Startup
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(Startup));

        private readonly IContainer container;
        private readonly ServiceBase host;
        private readonly Settings settings;
        private readonly EmbeddableDocumentStore documentStore;
        private readonly BusConfiguration configuration;
        private readonly ExposeBus exposeBus;

        public Startup(IContainer container, ServiceBase host, Settings settings, EmbeddableDocumentStore documentStore, BusConfiguration configuration, ExposeBus exposeBus)
        {
            this.container = container;
            this.host = host;
            this.settings = settings;
            this.documentStore = documentStore;
            this.configuration = configuration;
            this.exposeBus = exposeBus;
        }

        public void Configuration(IAppBuilder app)
        {
            var signalrIsReady = new SignalrIsReady();

            app.UseNServiceBus(settings, container, host, documentStore, configuration, exposeBus, signalrIsReady);

            if (settings.SetupOnly)
            {
                return;
            }

            app.Map("/api", b =>
            {
                b.Use<LogApiCalls>();

                ConfigureSignalR(b, signalrIsReady);

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
                var virtualDirectory = "storage";

                if (!String.IsNullOrEmpty(settings.VirtualDirectory))
                {
                    virtualDirectory = settings.VirtualDirectory + "/" + virtualDirectory;
                }

                ConfigureWindowsAuth(app, virtualDirectory);

                var configuration = new RavenConfiguration
                {
                    VirtualDirectory = virtualDirectory,
                    DataDirectory = Path.Combine(settings.DbPath, "Databases", "System"),
                    CompiledIndexCacheDirectory = Path.Combine(settings.DbPath, "CompiledIndexes"),
                    CountersDataDirectory = Path.Combine(settings.DbPath, "Data", "Counters"),
                    WebDir = Path.Combine(settings.DbPath, "Raven", "WebUI"),
                    PluginsDirectory = Path.Combine(settings.DbPath, "Plugins"),
                    AssembliesDirectory = Path.Combine(settings.DbPath, "Assemblies"),
                    AnonymousUserAccessMode = AnonymousUserAccessMode.None,
                    TurnOffDiscoveryClient = true,
                };

                var localRavenLicense = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RavenLicense.xml");
                if (File.Exists(localRavenLicense))
                {
                    Logger.Info($"Loading RavenDB license found from {localRavenLicense}");
                    configuration.Settings["Raven/License"] = NonLockingFileReader.ReadAllTextWithoutLocking(localRavenLicense);
                }
                else
                {
                    Logger.Info("Loading Embedded RavenDB license");
                    configuration.Settings["Raven/License"] = ReadLicense();
                }

                configuration.FileSystem.DataDirectory = Path.Combine(settings.DbPath, "FileSystems");
                configuration.FileSystem.IndexStoragePath = Path.Combine(settings.DbPath, "FileSystems", "Indexes");

                app.UseRavenDB(new RavenDBOptions(configuration));
            });
        }

        private static void ConfigureWindowsAuth(IAppBuilder app, string virtualDirectory)
        {
            var pathToLookFor = "/" + virtualDirectory;
            var listener = (HttpListener)app.Properties["System.Net.HttpListener"];
            listener.AuthenticationSchemes = AuthenticationSchemes.IntegratedWindowsAuthentication | AuthenticationSchemes.Anonymous;
            listener.AuthenticationSchemeSelectorDelegate += request =>
            {
                if (!request.Url.AbsolutePath.StartsWith(pathToLookFor, StringComparison.InvariantCultureIgnoreCase))
                {
                    return AuthenticationSchemes.Anonymous;
                }

                var hasSingleUseToken = string.IsNullOrEmpty(request.Headers["Single-Use-Auth-Token"]) == false || string.IsNullOrEmpty(request.QueryString["singleUseAuthToken"]) == false;

                return hasSingleUseToken ? AuthenticationSchemes.Anonymous : AuthenticationSchemes.IntegratedWindowsAuthentication;
            };
        }

        private void ConfigureSignalR(IAppBuilder app, SignalrIsReady signalrIsReady)
        {
            var resolver = new AutofacDependencyResolver(container);

            app.Map("/messagestream", map =>
            {
                map.UseCors(CorsOptions.AllowAll);
                map.RunSignalR<MessageStreamerConnection>(
                    new ConnectionConfiguration
                    {
                        EnableJSONP = true,
                        Resolver = resolver
                    });
            });

            GlobalHost.DependencyResolver = resolver;

            var jsonSerializer = JsonSerializer.Create(SerializationSettingsFactoryForSignalR.CreateDefault());
            GlobalHost.DependencyResolver.Register(typeof(JsonSerializer), () => jsonSerializer);

            signalrIsReady.Ready = true;
        }
    }

    class AutofacDependencyResolver : DefaultDependencyResolver
    {
        static Type IEnumerableType = typeof(IEnumerable<>);

        private readonly IContainer container;

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
            
            if (container.TryResolve(IEnumerableType.MakeGenericType(serviceType), out services))
            {
                return (IEnumerable<object>) services;
            }

            return base.GetServices(serviceType);
        }
    }
}