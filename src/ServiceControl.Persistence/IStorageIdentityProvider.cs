namespace ServiceControl.Persistence;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Identifies the storage this instance writes to, so that it can be compared with what an audit
/// remote reports over the environment endpoint. The raw values never leave the process; only
/// hashes are compared, and only the comparison result is reported.
/// </summary>
public interface IStorageIdentityProvider
{
    /// <summary>
    /// Null when the storage could not be asked, which the comparison reports as Unknown rather
    /// than guessing from configuration that the two sides may spell differently.
    /// </summary>
    ValueTask<StorageIdentity?> GetIdentity(CancellationToken cancellationToken = default);
}

public record StorageIdentity(string Engine, string Server, string Database, string? Schema);
