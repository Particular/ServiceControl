using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence.RavenDb5;
using TestHelper;

static class SharedEmbeddedServer
{
    public static async Task<EmbeddedDatabase> GetInstance(CancellationToken cancellationToken = default)
    {
        var basePath = Path.Combine(Path.GetTempPath(), "ServiceControlTests", "Primary.Raven5Acceptance");

        var settings = new RavenDBPersisterSettings
        {
            DatabasePath = Path.Combine(basePath, "DB"),
            LogPath = Path.Combine(basePath, "Logs"),
            LogsMode = "Operations",
            ServerUrl = $"http://localhost:{PortUtility.FindAvailablePort(33334)}"
        };

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
}