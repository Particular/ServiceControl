namespace ServiceControl.Migration.Tests;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using ServiceControl.Persistence;
using ServiceControl.RavenDB;
using TestHelper;

/// <summary>
/// One embedded RavenDB for the whole assembly, holding a seeded database that stands in for a
/// customer's old instance. EmbeddedServer.Instance is process wide, so there can only be one.
/// </summary>
static class MigrationSourceServer
{
    public static string ServerUrl { get; private set; }

    public static string PrimaryDatabase { get; private set; }

    public static string ThroughputDatabase { get; private set; }

    public static async Task Start(CancellationToken cancellationToken = default)
    {
        var dbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "MigrationSource", "Data");
        var logPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "MigrationSource", "Logs");
        var port = PortUtility.GetAssignedOrAvailablePort(33377);

        ServerUrl = $"http://localhost:{port}";
        PrimaryDatabase = "primary";
        ThroughputDatabase = "throughput";

        var configuration = new EmbeddedDatabaseConfiguration(ServerUrl, PrimaryDatabase, dbPath, logPath, "Operations") { RunInMemory = true };
        database = EmbeddedDatabase.Start(configuration, lifetime);

        using var store = await database.Connect(cancellationToken);

        await store.Maintenance.Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord(PrimaryDatabase)), cancellationToken);
        await store.Maintenance.Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord(ThroughputDatabase)), cancellationToken);

        using var seeder = new DocumentStore
        {
            Urls = [ServerUrl],
            Database = PrimaryDatabase,
            Conventions = new DocumentConventions { SaveEnumsAsIntegers = true }
        }.Initialize();

        using var session = seeder.OpenAsyncSession();
        await session.StoreAsync(new EndpointSettings { Name = "Sales", TrackInstances = true }, "EndpointSettings/1", cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }

    public static async Task Stop()
    {
        if (database is null)
        {
            return;
        }

        await database.Stop(CancellationToken.None);
        database.Dispose();
        database = null;
        lifetime.StopApplication();
    }

    static EmbeddedDatabase database;
    static readonly TestLifetime lifetime = new();

    sealed class TestLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => stopping.Cancel();

        readonly CancellationTokenSource stopping = new();
    }
}

[SetUpFixture]
public class MigrationSourceServerFixture
{
    [OneTimeSetUp]
    public Task StartSource() => MigrationSourceServer.Start();

    [OneTimeTearDown]
    public Task StopSource() => MigrationSourceServer.Stop();
}
