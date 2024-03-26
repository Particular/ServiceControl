using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Raven.Client.Documents;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Persistence.RavenDB;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Persistence.Tests;
using ServiceControl.Persistence.UnitOfWork;

//[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public abstract class PersistenceTestBase
{
    string databaseName;
    EmbeddedDatabase embeddedServer;
    IHost host;

    [SetUp]
    public async Task SetUp()
    {
        databaseName = Guid.NewGuid().ToString("n");
        var retentionPeriod = TimeSpan.FromMinutes(1);

        await TestContext.Out.WriteLineAsync($"Test Database Name: {databaseName}");

        var hostBuilder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            // Force the DI container to run the dependency resolution check to verify all dependencies can be resolved
            EnvironmentName = Environments.Production
        });

        embeddedServer = await SharedEmbeddedServer.GetInstance();

        PersistenceSettings = new RavenPersisterSettings
        {
            AuditRetentionPeriod = retentionPeriod,
            ErrorRetentionPeriod = retentionPeriod,
            EventsRetentionPeriod = retentionPeriod,
            DatabaseName = databaseName,
            ConnectionString = embeddedServer.ServerUrl
        };

        var persistence = new RavenPersistenceConfiguration().Create(PersistenceSettings);

        persistence.AddPersistence(hostBuilder.Services);
        persistence.AddInstaller(hostBuilder.Services);

        RegisterServices.Invoke(hostBuilder.Services);

        host = hostBuilder.Build();

        await GetRequiredService<IPersistenceLifecycle>().Initialize();

        await host.StartAsync();

        CompleteDatabaseOperation();
    }

    [TearDown]
    public async Task TearDown()
    {
        // Needs to go first or database will be disposed
        await embeddedServer.DeleteDatabase(databaseName);

        await host.StopAsync();
        host.Dispose();
    }

    protected PersistenceSettings PersistenceSettings
    {
        get;
        private set;
    }

    protected T GetRequiredService<T>() => host.Services.GetRequiredService<T>();
    protected object GetRequiredService(Type serviceType) => host.Services.GetRequiredService(serviceType);

    protected Action<IServiceCollection> RegisterServices { get; set; } = _ => { };

    protected void CompleteDatabaseOperation() => GetRequiredService<IDocumentStore>().WaitForIndexing();

    protected async Task WaitUntil(Func<Task<bool>> conditionChecker, string condition, TimeSpan timeout = default)
    {
        timeout = timeout == default ? TimeSpan.FromSeconds(10) : timeout;

        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            if (await conditionChecker())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new Exception($"{condition} has not been meet in defined timespan: {timeout})");
    }

    [Conditional("DEBUG")]
    protected void BlockToInspectDatabase()
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

    protected IErrorMessageDataStore ErrorStore => GetRequiredService<IErrorMessageDataStore>();
    protected IRetryDocumentDataStore RetryStore => GetRequiredService<IRetryDocumentDataStore>();
    protected IBodyStorage BodyStorage => GetRequiredService<IBodyStorage>();
    protected IRetryBatchesDataStore RetryBatchesStore => GetRequiredService<IRetryBatchesDataStore>();
    protected IErrorMessageDataStore ErrorMessageDataStore => GetRequiredService<IErrorMessageDataStore>();
    protected IMessageRedirectsDataStore MessageRedirectsDataStore => GetRequiredService<IMessageRedirectsDataStore>();
    protected IMonitoringDataStore MonitoringDataStore => GetRequiredService<IMonitoringDataStore>();
    protected IIngestionUnitOfWorkFactory UnitOfWorkFactory => GetRequiredService<IIngestionUnitOfWorkFactory>();
    protected ICustomChecksDataStore CustomChecks => GetRequiredService<ICustomChecksDataStore>();
    protected IArchiveMessages ArchiveMessages => GetRequiredService<IArchiveMessages>();
    protected IIngestionUnitOfWorkFactory IngestionUnitOfWorkFactory => GetRequiredService<IIngestionUnitOfWorkFactory>();
    protected IEventLogDataStore EventLogDataStore => GetRequiredService<IEventLogDataStore>();
    protected IRetryDocumentDataStore RetryDocumentDataStore => GetRequiredService<IRetryDocumentDataStore>();
}