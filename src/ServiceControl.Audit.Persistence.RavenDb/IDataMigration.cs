namespace ServiceControl.Audit.Infrastructure.Migration
{
    using System.Threading.Tasks;

    interface IDataMigration
    {
        Task Migrate(int pageSize = 1024);
    }
}