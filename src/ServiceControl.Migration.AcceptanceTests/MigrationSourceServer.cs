namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using ServiceControl.RavenDB;
using TestHelper;

// Its own copy rather than SharedEmbeddedServer: that one uses the RavenDB persister's own settings type, which this project cannot reference because it loads the persister from its manifest instead.
static class MigrationSourceServer
{
    public static async Task<EmbeddedDatabase> GetInstance(CancellationToken cancellationToken = default)
    {
        await startLock.WaitAsync(cancellationToken);
        try
        {
            if (server != null)
            {
                return server;
            }

            var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "MigrationSource");
            var logPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Logs", "MigrationSource");
            var port = PortUtility.GetAssignedOrAvailablePort(33350);

            var configuration = new EmbeddedDatabaseConfiguration($"http://localhost:{port}", "primary", dbPath, logPath, "Operations") { RunInMemory = true };

            server = EmbeddedDatabase.Start(configuration, lifetime);
            return server;
        }
        finally
        {
            startLock.Release();
        }
    }

    public static async Task Stop()
    {
        await startLock.WaitAsync();
        try
        {
            if (server is null)
            {
                return;
            }

            await server.Stop(CancellationToken.None);
            server.Dispose();
            server = null;
            lifetime.StopApplication();
        }
        finally
        {
            startLock.Release();
        }
    }

    public static async Task<(string ServerUrl, string PrimaryDatabase, string ThroughputDatabase)> CreateDatabases(CancellationToken cancellationToken = default)
    {
        var instance = await GetInstance(cancellationToken);
        var primary = $"sc_src_{Guid.NewGuid():n}";
        var throughput = $"{primary}-throughput";

        using var store = await instance.Connect(cancellationToken);
        foreach (var name in new[] { primary, throughput })
        {
            await store.Maintenance.Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord(name)), cancellationToken);
        }

        return (instance.ServerUrl, primary, throughput);
    }

    static EmbeddedDatabase server;
    static readonly ApplicationLifetime lifetime = new(new NullLogger<ApplicationLifetime>());
    static readonly SemaphoreSlim startLock = new(1, 1);
}
