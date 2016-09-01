namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Security.Principal;
    using System.Text;
    using System.Threading;
    using Autofac;
    using global::ServiceControl.Infrastructure;
    using global::ServiceControl.Infrastructure.RavenDB;
    using Microsoft.Owin.Hosting;
    using NServiceBus;
    using Raven.Abstractions.Connection;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using Raven.Client.Document;
    using Raven.Client.Indexes;
    using Raven.Database.Config;
    using Raven.Database.Server;
    using Raven.Database.Server.Security.Windows;
    using Raven.Json.Linq;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceBus.Management.Infrastructure.Settings;

    public class SetupBootstrapper
    {
        private readonly Settings settings;
        private readonly HttpMessageHandler handler;

        public SetupBootstrapper(Settings settings, HttpMessageHandler handler = null)
        {
            this.settings = settings;
            this.handler = handler;
        }

        public void Run(string username)
        {
            var startup = new Startup(null, settings);
            using (WebApp.Start(new StartOptions(settings.RootUrl), builder => startup.ConfigureRavenDB(builder)))
            {
                CreateDatabase(username);
                InitialiseDatabase();

                var configuration = new BusConfiguration();
                configuration.AssembliesToScan(AllAssemblies.Except("ServiceControl.Plugin"));
                configuration.EnableInstallers();

                var containerBuilder = new ContainerBuilder();
                var documentStore = new DocumentStore();
                containerBuilder.RegisterInstance(documentStore).As<IDocumentStore>().ExternallyOwned();
                containerBuilder.RegisterInstance(settings);

                NServiceBusFactory.Create(settings, containerBuilder.Build(), null, documentStore, configuration).Dispose();
            }
        }

        public void InitialiseDatabase()
        {
            var documentStore = new DocumentStore
            {
                Url = settings.StorageUrl,
                DefaultDatabase = "ServiceControl",
                Conventions =
                {
                    SaveEnumsAsIntegers = true
                },
                Credentials = CredentialCache.DefaultNetworkCredentials
            };

            if (handler != null)
            {
                documentStore.HttpMessageHandlerFactory = () => handler;
            }

            using (documentStore.Initialize())
            {
                Console.Out.WriteLine("Index creation started");

                using (var intercepter = new UpdatingSchemaInterceptor(Console.Out))
                {
                    while (true)
                    {
                        try
                        {
                            IndexCreation.CreateIndexes(typeof(RavenBootstrapper).Assembly, documentStore);

                            break;
                        }
                        catch (ErrorResponseException)
                        {
                            if (!intercepter.Updating)
                            {
                                Console.Error.WriteLine("Failed to create indexes, waiting to try again.");
                            }
                        }

                        Thread.Sleep(1000);
                    }
                }
            }
        }

        public void CreateDatabase(string username)
        {
            var documentStore = new DocumentStore
            {
                Url = settings.StorageUrl,
                Credentials = CredentialCache.DefaultNetworkCredentials
            };

            if (handler != null)
            {
                documentStore.HttpMessageHandlerFactory = () => handler;
            }

            using (documentStore.Initialize())
            {
                try
                {
                    Console.Out.WriteLine("Creating database...");

                    documentStore.DatabaseCommands.GlobalAdmin.CreateDatabase(new DatabaseDocument
                    {
                        Id = "ServiceControl",
                        Settings =
                        {
                            {"Raven/StorageTypeName", InMemoryRavenConfiguration.VoronTypeName},
                            {"Raven/DataDir", Path.Combine(settings.DbPath, "Databases", "ServiceControl")},
                            {"Raven/Counters/DataDir", Path.Combine(settings.DbPath, "Data", "Counters")},
                            {"Raven/WebDir", Path.Combine(settings.DbPath, "Raven", "WebUI")},
                            {"Raven/PluginsDirectory", Path.Combine(settings.DbPath, "Plugins")},
                            {"Raven/AssembliesDirectory", Path.Combine(settings.DbPath, "Assemblies")},
                            {"Raven/CompiledIndexCacheDirectory", Path.Combine(settings.DbPath, "CompiledIndexes")},
                            {"Raven/FileSystem/DataDir", Path.Combine(settings.DbPath, "FileSystems")},
                            {"Raven/FileSystem/IndexStoragePath", Path.Combine(settings.DbPath, "FileSystems", "Indexes")},
                            {"Raven/AnonymousAccess", AnonymousUserAccessMode.None.ToString()}
                        }
                    });

                    var windowsAuthDocument = new WindowsAuthDocument();
                    var localAdministratorsGroupName = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Translate(typeof(NTAccount)).ToString();
                    var localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Translate(typeof(NTAccount)).ToString();

                    var group = new WindowsAuthData
                    {
                        Enabled = true,
                        Name = localAdministratorsGroupName
                    };
                    group.Databases.Add(new ResourceAccess
                    {
                        Admin = true,
                        ReadOnly = false,
                        TenantId = "ServiceControl"
                    });
                    group.Databases.Add(new ResourceAccess
                    {
                        Admin = true,
                        ReadOnly = false,
                        TenantId = "<system>"
                    });
                    windowsAuthDocument.RequiredGroups.Add(group);

                    var user = new WindowsAuthData
                    {
                        Enabled = true,
                        Name = username ?? localSystem
                    };
                    user.Databases.Add(new ResourceAccess
                    {
                        Admin = false,
                        ReadOnly = false,
                        TenantId = "ServiceControl"
                    });
                    windowsAuthDocument.RequiredUsers.Add(user);

                    var ravenJObject = RavenJObject.FromObject(windowsAuthDocument);

                    documentStore.DatabaseCommands.ForSystemDatabase().Put("Raven/Authorization/WindowsSettings", null, ravenJObject, new RavenJObject());

                    Console.Out.WriteLine("Database created and secured.");
                }
                catch (Exception)
                {
                    Console.Out.WriteLine("Database already exists.");
                }
            }
        }

        class UpdatingSchemaInterceptor : TextWriter
        {
            private readonly TextWriter originalOut;
            public bool Updating;

            public UpdatingSchemaInterceptor(TextWriter originalOut)
            {
                this.originalOut = originalOut;
                Encoding = originalOut.Encoding;
                Console.SetOut(this);
            }

            public override Encoding Encoding { get; }

            public override void Write(string value)
            {
                if (value.StartsWith("Updating schema"))
                {
                    Updating = true;
                }
                originalOut.Write(value);
            }

            public override void Write(char value)
            {
                originalOut.Write(value);
            }

            public override void WriteLine(string value)
            {
                if (value.StartsWith("Updating schema"))
                {
                    Updating = true;
                }
                originalOut.WriteLine(value);
            }

            protected override void Dispose(bool disposing)
            {
                Console.SetOut(originalOut);
                base.Dispose(disposing);
            }
        }
    }
}