namespace ServiceControl.Audit.Infrastructure.Migration
{
    using System.Threading;
    using System.Threading.Tasks;

    interface IDataMigration
    {
        Task Migrate(int pageSize = 1024, CancellationToken cancellationToken = default);
    }
}