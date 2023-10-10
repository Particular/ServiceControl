using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus;
using NUnit.Framework;
using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Persistence.UnitOfWork;
using ServiceControl.PersistenceTests;

//[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public abstract class PersistenceTestBase
{
    IHost host;
    readonly TestPersistence testPersistence = new TestPersistenceImpl();

    [SetUp]
    public async Task SetUp()
    {
        var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddConsole();
            }).ConfigureServices(services =>
            {
                services.AddSingleton<IDomainEvents, FakeDomainEvents>();
                services.AddSingleton(new CriticalError(null));

                testPersistence.Configure(services);
                RegisterServices?.Invoke(services);
            });

        host = hostBuilder.Build();

        var persistenceLifecycle = GetRequiredService<IPersistenceLifecycle>();
        await persistenceLifecycle.Initialize();
        await host.StartAsync();

        CompleteDatabaseOperation();
    }

    [TearDown]
    public async Task TearDown()
    {
        // Needs to go first or database will be disposed
        await testPersistence.TearDown();

        await host.StopAsync();
        host.Dispose();
    }

    protected PersistenceSettings PersistenceSettings => testPersistence.Settings;

    protected T GetRequiredService<T>() => host.Services.GetRequiredService<T>();
    protected object GetRequiredService(Type serviceType) => host.Services.GetRequiredService(serviceType);

    protected Action<IServiceCollection> RegisterServices { get; set; }

    protected void CompleteDatabaseOperation() => testPersistence.CompleteDatabaseOperation();

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
    protected void BlockToInspectDatabase() => testPersistence.BlockToInspectDatabase();

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