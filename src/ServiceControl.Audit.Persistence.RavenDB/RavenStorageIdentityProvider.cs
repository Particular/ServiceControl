namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    class RavenStorageIdentityProvider(DatabaseConfiguration databaseConfiguration) : IStorageIdentityProvider
    {
        public ValueTask<StorageIdentity> GetIdentity(CancellationToken cancellationToken = default)
        {
            var serverConfiguration = databaseConfiguration.ServerConfiguration;
            var configuredServer = serverConfiguration.UseEmbeddedServer ? serverConfiguration.ServerUrl : serverConfiguration.ConnectionString;

            return new ValueTask<StorageIdentity>(new StorageIdentity("RavenDB", NormalizeServer(configuredServer), databaseConfiguration.Name, null));
        }

        static string NormalizeServer(string configuredServer) =>
            Uri.TryCreate(configuredServer, UriKind.Absolute, out var url) ? $"{url.Host}:{url.Port}" : configuredServer ?? "";
    }
}
