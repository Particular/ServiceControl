namespace ServiceControl.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Lets a host that never migrates the database, an ingestion worker, refuse to start against a
    /// database the owner has not migrated yet, rather than failing on its first write. Registered
    /// by persisters that keep a migration history; absent on the rest.
    /// </summary>
    public interface IDatabaseSchemaProbe
    {
        Task EnsureCurrent(CancellationToken cancellationToken = default);
    }
}
