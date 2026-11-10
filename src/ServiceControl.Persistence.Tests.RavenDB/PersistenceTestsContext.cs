namespace ServiceControl.Persistence.Tests;

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.Documents.Session;
using ServiceControl.Contracts.Operations;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence;
using ServiceControl.Persistence.RavenDB;
using ServiceControl.RavenDB;
using TestHelper;

public class PersistenceTestsContext : IPersistenceTestsContext
{
    string databaseName;
    EmbeddedDatabase embeddedServer;

    public async Task Setup(IHostApplicationBuilder hostBuilder)
    {
        databaseName = Guid.NewGuid().ToString("n");
        var retentionPeriod = TimeSpan.FromMinutes(1);

        await TestContext.Out.WriteLineAsync($"Test Database Name: {databaseName}");

        embeddedServer = await SharedEmbeddedServer.GetInstance();

        PersistenceSettings = new RavenPersisterSettings
        {
            AuditRetentionPeriod = retentionPeriod,
            ErrorRetentionPeriod = retentionPeriod,
            EventsRetentionPeriod = retentionPeriod,
            DatabaseName = databaseName,
            ConnectionString = embeddedServer.ServerUrl,
            ThroughputDatabaseName = $"{databaseName}-throughput",
        };

        var persistence = new RavenPersistenceConfiguration().Create(PersistenceSettings);

        hostBuilder.Services.AddSingleton<TimeProvider>(FakeTime);

        persistence.AddPersistence(hostBuilder.Services);
        persistence.AddInstaller(hostBuilder.Services);
    }

    public async Task PostSetup(IHost host)
    {
        DocumentStore = await host.Services.GetRequiredService<IRavenDocumentStoreProvider>().GetDocumentStore();
        SessionProvider = host.Services.GetRequiredService<IRavenSessionProvider>();

        await CompleteDatabaseOperation();
    }

    public async Task TearDown()
    {
        if (embeddedServer == null)
        {
            return;
        }

        // A dynamic query makes RavenDB create an auto index, which has no per-index search engine setting, so the search
        // engine custom check keeps reporting it after all static indexes use Lucene. Query through a static index instead.
        var autoIndexes = DocumentStore == null
            ? []
            : (await DocumentStore.Maintenance.SendAsync(new GetIndexNamesOperation(0, int.MaxValue)))
                .Where(name => name.StartsWith("Auto/", StringComparison.OrdinalIgnoreCase))
                .ToArray();

        await embeddedServer.DeleteDatabase(databaseName);
        await embeddedServer.DeleteDatabase(((RavenPersisterSettings)PersistenceSettings).ThroughputDatabaseName);

        Assert.That(autoIndexes, Is.Empty, "A query in the error database created an auto index");
    }

    public PersistenceSettings PersistenceSettings { get; private set; }
    public string GenerateFailedMessageRecordId(string messageId) => FailedMessageIdGenerator.MakeDocumentId(messageId);
    public async Task InsertFailedMessages(params FailedMessage[] messages)
    {
        using var session = await SessionProvider.OpenSession();
        foreach (var message in messages)
        {
            await session.StoreAsync(message);
        }
        await session.SaveChangesAsync();
    }

    public IDocumentStore DocumentStore { get; private set; }

    public IRavenSessionProvider SessionProvider { get; private set; }

    public FakeTimeProvider FakeTime { get; } = new(DateTimeOffset.UtcNow);

    // Moves only what the persister stamps itself. The Raven server expires documents on its own
    // clock, so advancing this changes the @expires value written, never when the server acts on it.
    public void AdvanceClock(TimeSpan by) => FakeTime.Advance(by);

    public DateTime UtcNow => FakeTime.GetUtcNow().UtcDateTime;

    public Task CompleteDatabaseOperation() => DocumentStore.WaitForIndexingAsync();

    [Conditional("DEBUG")]
    public void BlockToInspectDatabase()
    {
        if (!Debugger.IsAttached)
        {
            return;
        }

        var url = embeddedServer.ServerUrl + "/studio/index.html#databases/documents?&database=" + databaseName;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            url = url.Replace("&", "^&");
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("xdg-open", url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }

        Debugger.Break();
    }
}