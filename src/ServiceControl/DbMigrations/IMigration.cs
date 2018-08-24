namespace Particular.ServiceControl.DbMigrations
{
    using Raven.Client.Documents;

    public interface IMigration
    {
        string MigrationId { get; }
        string Apply(IDocumentStore store);
    }
}