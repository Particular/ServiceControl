namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnitOfWork;

    [TestFixture]
    class PersistenceTestFixture
    {
        [SetUp]
        public Task Setup()
        {
            configuration = new PersistenceTestsConfiguration();

            return configuration.Configure();
        }

        [TearDown]
        public Task Cleanup()
        {
            return configuration.Cleanup();
        }

        protected IAuditDataStore DataStore => configuration.AuditDataStore;

        protected IFailedAuditStorage FailedAuditStorage => configuration.FailedAuditStorage;

        protected IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory =>
            configuration.AuditIngestionUnitOfWorkFactory;

        protected IAuditIngestionUnitOfWork StartAuditUnitOfWork(int batchSize) =>
            AuditIngestionUnitOfWorkFactory.StartNew(batchSize);

        protected PersistenceTestsConfiguration configuration;
    }
}