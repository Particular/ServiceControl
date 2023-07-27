namespace ServiceControl.PersistenceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NUnit.Framework;
    using Persistence;
    using Persistence.MessageRedirects;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Persistence.UnitOfWork;

    [TestFixtureSource(typeof(PersistenceTestCollection))]
    abstract class PersistenceTestBase
    {
        readonly TestPersistence persistence;
        ServiceProvider serviceProvider;
        IHostedService[] hostedServices;

        protected PersistenceTestBase(TestPersistence persistence)
        {
            this.persistence = persistence;
        }

        [SetUp]
        public virtual async Task Setup()
        {
            var services = new ServiceCollection();
            await persistence.Configure(services);
            serviceProvider = services.BuildServiceProvider();

            await HostedServicesStart();
        }


        [TearDown]
        public async Task Cleanup()
        {
            await persistence.CleanupDB();

            await HostedServicesStop();

            await serviceProvider.DisposeAsync();
        }

        async Task HostedServicesStart()
        {
            // TODO: Combine with logic in PersistenceTestBase
            hostedServices = serviceProvider.GetServices<IHostedService>().ToArray();
            foreach (var service in hostedServices)
            {
                await service.StartAsync(default);
            }
        }

        async Task HostedServicesStop()
        {
            foreach (var service in hostedServices.Reverse())
            {
                await service.StopAsync(default);
            }
        }

        protected Task CompleteDBOperation() => persistence.CompleteDBOperation();

        T GetRequiredService<T>() => serviceProvider.GetRequiredService<T>();

        protected IErrorMessageDataStore ErrorStore => GetRequiredService<IErrorMessageDataStore>();
        protected IRetryDocumentDataStore RetryStore => GetRequiredService<IRetryDocumentDataStore>();
        protected IBodyStorage BodyStorage => GetRequiredService<IBodyStorage>();
        protected IRetryBatchesDataStore RetryBatchesStore => GetRequiredService<IRetryBatchesDataStore>();
        protected IErrorMessageDataStore ErrorMessageDataStore => GetRequiredService<IErrorMessageDataStore>();
        protected IMessageRedirectsDataStore MessageRedirectsDataStore => GetRequiredService<IMessageRedirectsDataStore>();
        protected IMonitoringDataStore MonitoringDataStore => GetRequiredService<IMonitoringDataStore>();
        protected IIngestionUnitOfWorkFactory UnitOfWorkFactory => GetRequiredService<IIngestionUnitOfWorkFactory>();
        protected ICustomChecksDataStore CustomChecks => GetRequiredService<ICustomChecksDataStore>();
    }
}
