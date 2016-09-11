namespace ServiceBus.Management.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using global::Nancy.Owin;
    using Microsoft.AspNet.SignalR;
    using Nancy;
    using Owin;
    using ServiceControl.Infrastructure.SignalR;
    using Autofac;
    using Metrics;
    using Microsoft.Owin.Cors;
    using NServiceBus.Logging;
    using Owin.Metrics;
    using Particular.ServiceControl.Licensing;
    using Raven.Database.Config;
    using Raven.Database.Server;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.OWIN;
    using JsonSerializer = Newtonsoft.Json.JsonSerializer;

    public class Startup
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(Startup));

        private readonly IContainer container;
        private readonly Settings settings;

        public Startup(IContainer container, Settings settings)
        {
            this.container = container;
            this.settings = settings;
        }

        public void Configuration(IAppBuilder app, bool isRunningAcceptanceTests = false)
        {
            ConfigureRavenDB(app, isRunningAcceptanceTests);

            app.Map("/metrics", b =>
            {
                Metric.Config
                    .WithOwin(middleware => b.Use(middleware), config => config
                        .WithMetricsEndpoint(endpointConfig => endpointConfig.MetricsEndpoint(String.Empty)))
                    .WithAllCounters();
            });

            app.Map("/api", b =>
            {
                b.Use<LogApiCalls>();

                ConfigureSignalR(b);

                b.UseNancy(new NancyOptions
                {
                    Bootstrapper = new NServiceBusContainerBootstrapper(container)
                });
            });
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

        public void ConfigureRavenDB(IAppBuilder builder, bool isRunningAcceptanceTests = false)
        {
            builder.Map("/storage", app =>
            {
                var virtualDirectory = "storage";

                if (!String.IsNullOrEmpty(settings.VirtualDirectory))
                {
                    virtualDirectory = $"{settings.VirtualDirectory}/{virtualDirectory}";
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
                    AnonymousUserAccessMode = isRunningAcceptanceTests ? AnonymousUserAccessMode.All : AnonymousUserAccessMode.None,
                    TurnOffDiscoveryClient = true,
                    MaxSecondsForTaskToWaitForDatabaseToLoad = 45 // So once the database grows, it takes longer to startup!
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
            if (!app.Properties.ContainsKey("System.Net.HttpListener"))
            {
                return;
            }

            var pathToLookFor = $"/{virtualDirectory}";
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

        private void ConfigureSignalR(IAppBuilder app)
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
