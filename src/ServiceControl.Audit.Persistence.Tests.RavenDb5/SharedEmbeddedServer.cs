﻿namespace ServiceControl.Audit.Persistence.Tests
{
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.RavenDb;
    using ServiceControl.Audit.Persistence.RavenDb5;

    class SharedEmbeddedServer
    {
        public static async Task<EmbeddedDatabase> GetInstance(CancellationToken cancellationToken = default)
        {
            if (embeddedDatabase != null)
            {
                return embeddedDatabase;
            }

            var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "AuditData");
            var serverUrl = $"http://localhost:{FindAvailablePort(33334)}";
            embeddedDatabase = EmbeddedDatabase.Start(dbPath, serverUrl, new AuditDatabaseConfiguration("audit"));

            await embeddedDatabase.Initialize(cancellationToken)
                .ConfigureAwait(false);

            return embeddedDatabase;
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

        static EmbeddedDatabase embeddedDatabase;
    }
}
