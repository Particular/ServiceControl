namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;

    partial class PersistenceTestsConfiguration
    {
        public IAuditDataStore AuditDataStore { get; protected set; }

        public Task Configure()
        {
            return Task.CompletedTask;
        }

        public Task CompleteDBOperation()
        {
            return Task.CompletedTask;
        }

        public Task Cleanup()
        {
            return Task.CompletedTask;
        }

        public override string ToString() => "SqlServer";
    }
}