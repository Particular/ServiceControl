namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using NUnit.Framework;

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

        protected PersistenceTestsConfiguration configuration;
    }
}