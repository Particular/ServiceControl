namespace ServiceControl.Persistence.Tests;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Raven.Client.Documents;
using ServiceControl.Persistence;
using ServiceControl.Persistence.RavenDB;
using ServiceControl.RavenDB;

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

        var settings = new RavenPersisterSettings
        {
            AuditRetentionPeriod = retentionPeriod,
            ErrorRetentionPeriod = retentionPeriod,
            EventsRetentionPeriod = retentionPeriod,
            DatabaseName = databaseName,
            ConnectionString = embeddedServer.ServerUrl,
            ThroughputDatabaseName = $"{databaseName}-throughput",
        };

        PersistenceSettings = settings;

        var persistence = new RavenPersistence(settings);

        persistence.AddPersistence(hostBuilder.Services);
        persistence.AddInstaller(hostBuilder.Services);
    }

    public async Task PostSetup(IHost host)
    {
        DocumentStore = await host.Services.GetRequiredService<IRavenDocumentStoreProvider>().GetDocumentStore();
        SessionProvider = host.Services.GetRequiredService<IRavenSessionProvider>();

        CompleteDatabaseOperation();
    }

    public async Task TearDown() => await embeddedServer.DeleteDatabase(databaseName);

    public PersistenceSettings PersistenceSettings { get; private set; }

    public IDocumentStore DocumentStore { get; private set; }

    public IRavenSessionProvider SessionProvider { get; private set; }

    public void CompleteDatabaseOperation() => DocumentStore.WaitForIndexing();

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