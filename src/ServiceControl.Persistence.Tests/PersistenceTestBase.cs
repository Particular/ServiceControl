using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
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
    ServiceProvider serviceProvider;

    [SetUp]
    public async Task SetUp()
    {
        databaseName = Guid.NewGuid().ToString("n");
        var retentionPeriod = TimeSpan.FromMinutes(1);

        await TestContext.Out.WriteLineAsync($"Test Database Name: {databaseName}");

        embeddedServer = await SharedEmbeddedServer.GetInstance(new MockHostApplicationLifetime());

        PersistenceSettings = new RavenPersisterSettings
        {
            AuditRetentionPeriod = retentionPeriod,
            ErrorRetentionPeriod = retentionPeriod,
            EventsRetentionPeriod = retentionPeriod,
            DatabaseName = databaseName,
            ConnectionString = embeddedServer.ServerUrl
        };

        var persistence = new RavenPersistenceConfiguration().Create(PersistenceSettings);

        var services = new ServiceCollection();

        services.ConfigurePersisterLifecyle(persistence);
        RegisterServices?.Invoke(services);

        serviceProvider = services.BuildServiceProvider();

        var persistenceLifecycle = GetRequiredService<IPersistenceLifecycle>();
        await persistenceLifecycle.Initialize();

        CompleteDatabaseOperation();
    }

    [TearDown]
    public async Task TearDown()
    {
        // Needs to go first or database will be disposed
        await embeddedServer.DeleteDatabase(databaseName);

        await serviceProvider.DisposeAsync();
    }

    protected PersistenceSettings PersistenceSettings
    {
        get;
        private set;
    }

    protected T GetRequiredService<T>() => serviceProvider.GetRequiredService<T>();
    protected object GetRequiredService(Type serviceType) => serviceProvider.GetRequiredService(serviceType);

    protected Action<IServiceCollection> RegisterServices { get; set; }

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