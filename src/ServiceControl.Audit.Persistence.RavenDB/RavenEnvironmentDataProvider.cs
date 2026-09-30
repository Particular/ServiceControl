namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents.Operations;
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
            Value("Storage.ServerEdition", () => "NotApplicable"),
            Value("Storage.ServiceObjective", () => "NotApplicable"),
            Deferred("Storage.SizeGB", SizeGB),
            Deferred("Storage.MessageCount", ProcessedMessageCount),
            Value("Storage.FullTextSearch", () => databaseConfiguration.EnableFullTextSearch ? "Enabled" : "Disabled")
        ];

        async ValueTask<string> SizeGB(CancellationToken cancellationToken)
        {
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            var statistics = await documentStore.Maintenance.ForDatabase(databaseConfiguration.Name).SendAsync(new GetStatisticsOperation(), cancellationToken);

            return (statistics.SizeOnDisk.SizeInBytes / BytesPerGigabyte).ToString("F1", CultureInfo.InvariantCulture);
        }

        async ValueTask<string> ProcessedMessageCount(CancellationToken cancellationToken)
        {
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            var statistics = await documentStore.Maintenance.ForDatabase(databaseConfiguration.Name).SendAsync(new GetCollectionStatisticsOperation(), cancellationToken);

            return statistics.Collections.TryGetValue("ProcessedMessages", out var count)
                ? count.ToString(CultureInfo.InvariantCulture)
                : "0";
        }

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

        const double BytesPerGigabyte = 1024d * 1024 * 1024;
    }
}
