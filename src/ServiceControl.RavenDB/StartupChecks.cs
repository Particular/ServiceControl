namespace ServiceControl.RavenDB
{
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Indexes;
    using Raven.Client.ServerWide.Operations;
    using ServiceControl.Infrastructure;

    public static class StartupChecks
    {
        public static async Task WarnIfIndexesUseCorax(IDocumentStore store, string databaseName, CancellationToken cancellationToken = default)
        {
            // New databases are created with Lucene, existing databases keep whatever search engine they were created
            // with as switching would trigger a full rebuild of all indexes. Let the operator know so they can plan the
            // transition to Lucene themselves.
            var coraxIndexes = await FindIndexesUsingCorax(store, databaseName, cancellationToken);

            if (coraxIndexes.Length > 0)
            {
                Logger.LogWarning(CoraxIndexesMessage(coraxIndexes.Select(i => $"{databaseName}/{i}")));
            }
        }

        public static string CoraxIndexesMessage(IEnumerable<string> indexes) =>
            $"The following RavenDB index(es) use the Corax search engine: {string.Join(", ", indexes)}. " +
            "Lucene indexes are smaller, use less memory and perform better for ServiceControl workloads, and are the default for new databases. " +
            "Consider switching these indexes to Lucene. Note that switching triggers a full rebuild of the index: on very large databases this can take days depending on the available compute, " +
            "and while the rebuild is running ingestion and indexing rates can be degraded. Plan the switch accordingly.";

        public static async Task<string[]> FindIndexesUsingCorax(IDocumentStore store, string databaseName, CancellationToken cancellationToken = default)
        {
            var indexStats = await store.Maintenance.ForDatabase(databaseName).SendAsync(new GetIndexesStatisticsOperation(), cancellationToken);

            return indexStats
                .Where(i => i.SearchEngineType == SearchEngineType.Corax)
                .Select(i => i.Name)
                .ToArray();
        }

        public static async Task EnsureServerVersion(IDocumentStore store, CancellationToken cancellationToken = default)
        {
            // RavenDB compatibility policy is that the major/minor version of the server must be
            // equal or higher than the client and ignores the patch version.
            //
            // https://docs.ravendb.net/6.2/client-api/faq/backward-compatibility/#compatibility---ravendb-42-and-higher
            //
            // > Starting with version 4.2, RavenDB clients are compatible with any server of their own version and higher.
            // > E.g. -
            // >
            // > Client 4.2 is compatible with Server 4.2, Server 4.5, Server 5.2, and any other server of a higher version.

            var build = await store.Maintenance.Server.SendAsync(new GetBuildNumberOperation(), cancellationToken);
            var serverProductVersion = new Version(build.ProductVersion);

            var clientVersion = typeof(Raven.Client.Constants).Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;
            var parts = clientVersion.Split('.');
            var clientProductVersion = new Version($"{parts[0]}.{parts[1]}");

            if (clientProductVersion > serverProductVersion)
            {
                throw new Exception($"ServiceControl expects RavenDB Server version {clientProductVersion} or higher, but the server is using {serverProductVersion}.");
            }
        }

        static readonly ILogger Logger = LoggerUtil.CreateStaticLogger(typeof(StartupChecks));
    }
}
