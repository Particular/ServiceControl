namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.ServerWide.Operations;
    using static ServiceControl.Audit.Persistence.EnvironmentDatum;

    class RavenEnvironmentDataProvider(DatabaseConfiguration databaseConfiguration, IRavenDocumentStoreProvider documentStoreProvider) : IEnvironmentDataProvider
    {
        public IEnumerable<EnvironmentDatum> GetData() =>
        [
            Value("Storage.Type", () => "RavenDB"),
            Value("Storage.RavenServer", () => databaseConfiguration.ServerConfiguration.UseEmbeddedServer ? "Embedded" : "External"),
            Value("Storage.Hosting", () => Hosting().Hosting),
            Deferred("Storage.ServerVersion", ServerVersion),
            Value("Storage.HostingSource", () => Hosting().Source),
            Value("Storage.FullTextSearch", () => databaseConfiguration.EnableFullTextSearch ? "Enabled" : "Disabled")
        ];

        (string Hosting, string Source) Hosting()
        {
            var serverConfiguration = databaseConfiguration.ServerConfiguration;

            // An embedded server runs in this process, so there is nothing to infer.
            if (serverConfiguration.UseEmbeddedServer)
            {
                return (DatabaseHostClassifier.SelfHosted, DatabaseHostingSource.Configuration);
            }

            return Uri.TryCreate(serverConfiguration.ConnectionString, UriKind.Absolute, out var url)
                ? (DatabaseHostClassifier.Classify(url.Host), DatabaseHostingSource.ConnectionString)
                : (DatabaseHostClassifier.Unknown, DatabaseHostingSource.None);
        }

        async ValueTask<string> ServerVersion(CancellationToken cancellationToken)
        {
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            var buildNumber = await documentStore.Maintenance.Server.SendAsync(new GetBuildNumberOperation(), cancellationToken);

            return buildNumber.ProductVersion ?? DatabaseHostClassifier.Unknown;
        }
    }
}
