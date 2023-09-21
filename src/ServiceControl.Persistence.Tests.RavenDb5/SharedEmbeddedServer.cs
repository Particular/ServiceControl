using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.RavenDb5;

static class SharedEmbeddedServer
{
    public static async Task<EmbeddedDatabase> GetInstance(CancellationToken cancellationToken = default)
    {
        var settings = new RavenDBPersisterSettings
        {
            DatabasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "ErrorData"),
            LogPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Logs"),
            LogsMode = "Operations",
            ServerUrl = $"http://localhost:{FindAvailablePort(33334)}"
        };

        Directory.Delete(settings.DatabasePath, true);

        var instance = EmbeddedDatabase.Start(settings);

        //make sure that the database is up
        while (true)
        {
            try
            {
                using (await instance.Connect(cancellationToken))
                {
                    //no-op
                }

                return instance;
            }
            catch (Exception)
            {
                await Task.Delay(500, cancellationToken);
            }
        }
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
}