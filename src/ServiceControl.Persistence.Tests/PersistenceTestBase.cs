namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus;
using NServiceBus.Settings;
using NUnit.Framework;
using Particular.LicensingComponent.Persistence;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.DomainEvents;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence;
using ServiceControl.Persistence.MessageRedirects;
using ServiceControl.Persistence.Recoverability;
using ServiceControl.Persistence.UnitOfWork;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public abstract class PersistenceTestBase
{
    IHost host;

    [SetUp]
    public async Task SetUp()
    {
        PersistenceTestsContext = new PersistenceTestsContext();
        if (PersistenceTestsContext is not IPersistenceTestsContext)
        {
            throw new Exception($"{nameof(PersistenceTestsContext)} must implement {nameof(IPersistenceTestsContext)}");
        }

        var hostBuilder = Host.CreateApplicationBuilder();

        LoggerUtil.ActiveLoggers = Loggers.Test;
        hostBuilder.Logging.BuildLogger(LogLevel.Information);

        await PersistenceTestsContext.Setup(hostBuilder);

        // This is not cool. We have things that are registered as part of "the persistence" that then require parts
        // of the infrastructure to be registered and assume NServiceBus is around. This is a hack to get around that.
        hostBuilder.Services.AddSingleton<IDomainEvents, FakeDomainEvents>();
        hostBuilder.Services.AddSingleton(new CriticalError((_, __) => Task.CompletedTask));
        hostBuilder.Services.AddSingleton<IReadOnlySettings>(new SettingsHolder());
        hostBuilder.Services.AddSingleton(new ReceiveAddresses("fakeReceiveAddress"));

        RegisterServices.Invoke(hostBuilder.Services);

        host = hostBuilder.Build();

        await host.StartAsync();
        await PersistenceTestsContext.PostSetup(host);
    }

    [TearDown]
    public async Task TearDown()
    {
        // Needs to go first or database will be disposed
        await PersistenceTestsContext.TearDown();
        await host.StopAsync();
        host.Dispose();
    }

    protected PersistenceTestsContext PersistenceTestsContext { get; private set; }

    protected PersistenceSettings PersistenceSettings => PersistenceTestsContext.PersistenceSettings;

    protected IServiceProvider ServiceProvider => host.Services;

    protected Action<IServiceCollection> RegisterServices { get; set; } = _ => { };

    protected void CompleteDatabaseOperation() => PersistenceTestsContext.CompleteDatabaseOperation();

    protected static async Task WaitUntil(Func<Task<bool>> conditionChecker, string condition, TimeSpan timeout = default)
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

    protected IErrorMessageDataStore ErrorStore => ServiceProvider.GetRequiredService<IErrorMessageDataStore>();
    protected IRetryDocumentDataStore RetryStore => ServiceProvider.GetRequiredService<IRetryDocumentDataStore>();
    protected IBodyStorage BodyStorage => ServiceProvider.GetRequiredService<IBodyStorage>();
    protected IRetryBatchesDataStore RetryBatchesStore => ServiceProvider.GetRequiredService<IRetryBatchesDataStore>();
    protected IErrorMessageDataStore ErrorMessageDataStore => ServiceProvider.GetRequiredService<IErrorMessageDataStore>();
    protected IMessageRedirectsDataStore MessageRedirectsDataStore => ServiceProvider.GetRequiredService<IMessageRedirectsDataStore>();
    protected IMonitoringDataStore MonitoringDataStore => ServiceProvider.GetRequiredService<IMonitoringDataStore>();
    protected IIngestionUnitOfWorkFactory UnitOfWorkFactory => ServiceProvider.GetRequiredService<IIngestionUnitOfWorkFactory>();
    protected ICustomChecksDataStore CustomChecks => ServiceProvider.GetRequiredService<ICustomChecksDataStore>();
    protected IArchiveMessages ArchiveMessages => ServiceProvider.GetRequiredService<IArchiveMessages>();
    protected IIngestionUnitOfWorkFactory IngestionUnitOfWorkFactory => ServiceProvider.GetRequiredService<IIngestionUnitOfWorkFactory>();
    protected IEventLogDataStore EventLogDataStore => ServiceProvider.GetRequiredService<IEventLogDataStore>();
    protected IRetryDocumentDataStore RetryDocumentDataStore => ServiceProvider.GetRequiredService<IRetryDocumentDataStore>();
    protected ILicensingDataStore LicensingDataStore => ServiceProvider.GetRequiredService<ILicensingDataStore>();
}