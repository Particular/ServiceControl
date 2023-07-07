namespace ServiceControl.Infrastructure.RavenDB
{
    using System.Threading.Tasks;
    using Raven.Client;

    interface IDataMigration
    {
        Task Migrate(IDocumentStore store);
    }
}