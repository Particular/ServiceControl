namespace Particular.ServiceControl.DbMigrations
{
    using Raven.Client;

    public interface IMigration
    {
        string MigrationId { get; }
        void Apply(IDocumentStore store);
    }
}