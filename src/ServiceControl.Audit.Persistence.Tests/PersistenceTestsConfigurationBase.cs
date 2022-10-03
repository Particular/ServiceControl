namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;

    class PersistenceTestsConfigurationBase
    {
        public virtual Task CompleteDBOperation() => Task.CompletedTask;

        public virtual Task Cleanup() => Task.CompletedTask;
    }
}