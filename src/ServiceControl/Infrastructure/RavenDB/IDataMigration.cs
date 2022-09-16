namespace ServiceControl.Infrastructure.RavenDB
{
    using System.Threading.Tasks;
    using Raven.Client;

    public interface IDataMigration
    {
        Task Migrate(IDocumentStore store);
    }
}