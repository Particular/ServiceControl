namespace ServiceControl.AcceptanceTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Persistence.RavenDB;
    using Persistence.Tests;
    using ServiceBus.Management.Infrastructure.Settings;

    public class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; } = typeof(RavenPersistenceConfiguration).AssemblyQualifiedName;

        public async Task CustomizeSettings(Settings settings)
        {
            databaseName = Guid.NewGuid().ToString("n");
            databaseInstance = await SharedEmbeddedServer.GetInstance(new MockHostApplicationLifetime());

            settings.PersisterSpecificSettings = new RavenPersisterSettings
            {
                ErrorRetentionPeriod = TimeSpan.FromDays(10),
                ConnectionString = databaseInstance.ServerUrl,
                DatabaseName = databaseName
            };
        }

        public async Task Cleanup()
        {
            if (databaseInstance == null)
            {
                return;
            }
            await databaseInstance.DeleteDatabase(databaseName);
        }

        EmbeddedDatabase databaseInstance;
        string databaseName;
    }

    class MockHostApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        readonly CancellationTokenSource startedToken = new();
        readonly CancellationTokenSource stoppedToken = new();
        readonly CancellationTokenSource stoppingToken = new();
        public void Started() => startedToken.Cancel();
        CancellationToken IHostApplicationLifetime.ApplicationStarted => startedToken.Token;
        CancellationToken IHostApplicationLifetime.ApplicationStopping => stoppingToken.Token;
        CancellationToken IHostApplicationLifetime.ApplicationStopped => stoppedToken.Token;
        public void Dispose()
        {
            stoppedToken.Cancel();
            startedToken.Dispose();
            stoppedToken.Dispose();
            stoppingToken.Dispose();
        }
        public void StopApplication() => stoppingToken.Cancel();
    }
}
