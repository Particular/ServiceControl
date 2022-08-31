namespace ServiceControl.Audit.Persistence.Tests
{
    partial class TestSuiteConstraints
    {
        public PersistenceTestFixture CreatePersistenceTestFixture() => new InMemory();
    }
}