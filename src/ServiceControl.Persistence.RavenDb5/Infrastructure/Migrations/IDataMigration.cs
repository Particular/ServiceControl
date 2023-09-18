namespace ServiceControl.Infrastructure.RavenDB
{
    using System.Threading.Tasks;
    using Raven.Client.Documents;

    interface IDataMigration
    {
        Task Migrate(IDocumentStore store);
    }
}