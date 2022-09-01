namespace ServiceControl.Audit.Persistence.SqlServer
{
    using System.Threading;
    using System.Threading.Tasks;

    class SqlDbPersistenceInstaller : IPersistenceInstaller
    {
        public Task Install(CancellationToken cancellationToken) => throw new System.NotImplementedException();
    }
}