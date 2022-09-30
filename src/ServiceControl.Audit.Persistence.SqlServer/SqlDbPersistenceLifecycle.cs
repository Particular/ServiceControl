namespace ServiceControl.Audit.Persistence.SqlServer
{
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence;

    class SqlDbPersistenceLifecycle : IPersistenceLifecycle
    {
        public Task Start(CancellationToken cancellationToken) => throw new System.NotImplementedException();
        public Task Stop(CancellationToken cancellationToken) => throw new System.NotImplementedException();
    }
}