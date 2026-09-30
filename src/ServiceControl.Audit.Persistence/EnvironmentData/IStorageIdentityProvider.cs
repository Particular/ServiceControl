namespace ServiceControl.Audit.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Identifies the storage this instance writes to, so that the primary instance can tell
    /// whether it shares a database or a database server with this one. The raw values never leave
    /// the process; the environment endpoint serves hashes of them.
    /// </summary>
    public interface IStorageIdentityProvider
    {
        ValueTask<StorageIdentity> GetIdentity(CancellationToken cancellationToken = default);
    }

    public record StorageIdentity(string Engine, string Server, string Database, string Schema);
}
