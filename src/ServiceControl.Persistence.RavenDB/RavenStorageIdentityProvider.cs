#nullable enable
namespace ServiceControl.Persistence.RavenDB;

using System;
using System.Threading;
using System.Threading.Tasks;

class RavenStorageIdentityProvider(RavenPersisterSettings settings) : IStorageIdentityProvider
{
    public ValueTask<StorageIdentity?> GetIdentity(CancellationToken cancellationToken = default)
    {
        var configuredServer = settings.UseEmbeddedServer ? settings.ServerUrl : settings.ConnectionString;

        return new ValueTask<StorageIdentity?>(new StorageIdentity("RavenDB", NormalizeServer(configuredServer), settings.DatabaseName, null));
    }

    // A loopback address names a different server on every machine, embedded servers included.
    internal static string NormalizeServer(string? configuredServer) =>
        Uri.TryCreate(configuredServer, UriKind.Absolute, out var url)
            ? $"{(url.IsLoopback ? Environment.MachineName : url.Host)}:{url.Port}"
            : configuredServer ?? "";
}
