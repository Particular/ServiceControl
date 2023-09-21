using System;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence;
using NServiceBus;
using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.PersistenceTests;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Persistence.UnitOfWork;

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
        await testPersistence.TearDown();
        await host.StopAsync();
        host.Dispose();
    }

    protected T GetRequiredService<T>() => host.Services.GetRequiredService<T>();

    protected Action<IServiceCollection> RegisterServices { get; set; }

    protected void CompleteDatabaseOperation() => testPersistence.CompleteDatabaseOperation();

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
}