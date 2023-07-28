namespace ServiceControl.PersistenceTests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NUnit.Framework;
    using Persistence;
    using Persistence.MessageRedirects;
    using Raven.Client;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Persistence.UnitOfWork;

    //[TestFixtureSource(typeof(PersistenceTestCollection))]
    abstract class PersistenceTestBase : BaseHostTest
    {
        TestPersistence testPersistence;

        protected override IHostBuilder CreateHostBuilder()
        {
            return base.CreateHostBuilder().ConfigureServices(services =>
            {
                services.AddSingleton(new CriticalError(null));
                testPersistence = new TestPersistenceImpl();
                testPersistence.Configure(services);
            });
        }

        [SetUp]
        public virtual void Setup()
        {
            GetRequiredService<IDocumentStore>().WaitForIndexing();
        }

        protected Task CompleteDatabaseOperation() => testPersistence.CompleteDatabaseOperation();

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