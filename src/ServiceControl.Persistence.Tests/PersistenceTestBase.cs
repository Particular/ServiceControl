using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBus.Settings;
using NUnit.Framework;
using Raven.Client.Documents;
using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Persistence.RavenDB;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Persistence.Tests;
using ServiceControl.Persistence.UnitOfWork;
using ServiceControl.PersistenceTests;

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

        var hostBuilder = Host.CreateApplicationBuilder();

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

        // This is not cool. We have things that are registered as part of "the persistence" that then require parts
        // of the infrastructure to be registered and assume NServiceBus is around. This is a hack to get around that.
        hostBuilder.Services.AddSingleton<IDomainEvents, FakeDomainEvents>();
        hostBuilder.Services.AddSingleton(new CriticalError((_, __) => Task.CompletedTask));
        hostBuilder.Services.AddSingleton<IReadOnlySettings>(new SettingsHolder());
        hostBuilder.Services.AddSingleton(new ReceiveAddresses("fakeReceiveAddress"));

        RegisterServices.Invoke(hostBuilder.Services);

        host = hostBuilder.Build();
        await host.StartAsync();

        DocumentStore = host.Services.GetRequiredService<IRavenDocumentStoreProvider>().GetDocumentStore();
        SessionProvider = host.Services.GetRequiredService<IRavenSessionProvider>();

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

    protected IDocumentStore DocumentStore { get; private set; }
    protected IRavenSessionProvider SessionProvider { get; private set; }

    protected T GetRequiredService<T>() => host.Services.GetRequiredService<T>();
    protected object GetRequiredService(Type serviceType) => host.Services.GetRequiredService(serviceType);

    protected Action<IServiceCollection> RegisterServices { get; set; } = _ => { };

    protected void CompleteDatabaseOperation() => DocumentStore.WaitForIndexing();

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