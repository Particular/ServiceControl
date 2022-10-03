namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;

    partial class PersistenceTestsOneTimeConfiguration
    {
        public Task SetUp() => Task.CompletedTask;

        public Task TearDown() => Task.CompletedTask;

        public PersistenceTestsConfiguration GetPerTestConfiguration()
        {
            return new PersistenceTestsConfiguration();
        }
    }
}
