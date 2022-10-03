namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;

    partial class PersistenceTestsOneTimeConfiguration
    {
        public Task SetUp()
        {
            return Task.CompletedTask;
        }

        public Task TearDown()
        {
            return Task.CompletedTask;
        }

        public PersistenceTestsConfiguration GetPerTestConfiguration()
        {
            return new PersistenceTestsConfiguration();
        }
    }
}
