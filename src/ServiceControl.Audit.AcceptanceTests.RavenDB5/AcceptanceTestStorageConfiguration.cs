namespace ServiceControl.Audit.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Raven.Embedded;
    using ServiceControl.Audit.Persistence.RavenDb;

    partial class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; protected set; }

        public void CustomizeSettings(IDictionary<string, string> settings)
        {
            var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "AuditData");
            Console.WriteLine($"DB Path: {dbPath}");

            settings["ServiceControl/Audit/RavenDb5/UseEmbeddedInstance"] = bool.TrueString;
            settings["ServiceControl.Audit/DbPath"] = dbPath;
            settings["ServiceControl.Audit/DatabaseMaintenancePort"] = FindAvailablePort(33334).ToString();
            settings["ServiceControl.Audit/HostName"] = "localhost";
        }

        static int FindAvailablePort(int startPort)
        {
            var activeTcpListeners = IPGlobalProperties
                .GetIPGlobalProperties()
                .GetActiveTcpListeners();

            for (var port = startPort; port < startPort + 1024; port++)
            {
                var portCopy = port;
                if (activeTcpListeners.All(endPoint => endPoint.Port != portCopy))
                {
                    return port;
                }
            }

            return startPort;
        }

        public Task Configure()
        {
            PersistenceType = typeof(RavenDbPersistenceConfiguration).AssemblyQualifiedName;

            return Task.CompletedTask;
        }

        public Task Cleanup()
        {
            EmbeddedServer.Instance.Dispose();

            return Task.CompletedTask;
        }
    }
}
