namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using System.Net;
    using System.Security.Principal;
    using Microsoft.Owin.Hosting;
    using NServiceBus;
    using Raven.Abstractions.Data;
    using Raven.Client.Document;
    using Raven.Database.Config;
    using Raven.Database.Server;
    using Raven.Database.Server.Security.Windows;
    using Raven.Json.Linq;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceBus.Management.Infrastructure.Settings;

    public class SetupBootstrapper : BootstrapperBase
    {
        public void Run(string username)
        {
            var startup = new Startup(null);
            using (WebApp.Start(new StartOptions(Settings.ApiUrl), startup.ConfigureRavenDB))
            {
                var configuration = new BusConfiguration();
                configuration.AssembliesToScan(AllAssemblies.Except("ServiceControl.Plugin"));
                configuration.EnableInstallers();
                CreateDatabase(username);
                ConfigureNServiceBus(configuration).Dispose();
            }
        }

        public static void CreateDatabase(string username)
        {
            using (var documentStore = new DocumentStore
            {
                Url = Settings.ApiUrl + "storage",
                Credentials = CredentialCache.DefaultNetworkCredentials
            }.Initialize())
            {
                try
                {
                    documentStore.DatabaseCommands.GlobalAdmin.CreateDatabase(new DatabaseDocument
                    {
                        Id = "ServiceControl",
                        Settings =
                        {
                            {"Raven/StorageTypeName", InMemoryRavenConfiguration.EsentTypeName},
                            {"Raven/DataDir", Path.Combine(Settings.DbPath, "Databases", "ServiceControl")},
                            {"Raven/Counters/DataDir", Path.Combine(Settings.DbPath, "Data", "Counters")},
                            {"Raven/WebDir", Path.Combine(Settings.DbPath, "Raven", "WebUI")},
                            {"Raven/PluginsDirectory", Path.Combine(Settings.DbPath, "Plugins")},
                            {"Raven/AssembliesDirectory", Path.Combine(Settings.DbPath, "Assemblies")},
                            {"Raven/CompiledIndexCacheDirectory", Path.Combine(Settings.DbPath, "CompiledIndexes")},
                            {"Raven/FileSystem/DataDir", Path.Combine(Settings.DbPath, "FileSystems")},
                            {"Raven/FileSystem/IndexStoragePath", Path.Combine(Settings.DbPath, "FileSystems", "Indexes")},
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

                    Console.Out.WriteLine("Database created and secured");
                }
                catch (Exception)
                {
                    Console.Out.WriteLine("Database already exists");
                }
            }
        }
    }
}