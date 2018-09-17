namespace Particular.ServiceControl.DbMigrations
{
    using Raven.Client;

    interface IMigration
    {
        string MigrationId { get; }
        string Apply(IDocumentStore store);
    }
}