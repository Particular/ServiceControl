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
            var build = await store.Maintenance.Server.SendAsync(new GetBuildNumberOperation(), cancellationToken);

            if (!TryValidateServerVersion(build.FullVersion, ClientVersion, out var error))
            {
                throw new Exception(error);
            }
        }

        public static bool TryValidateServerVersion(string serverFullVersion, Version clientVersion, out string error)
        {
            var clientMajorMinor = new Version(clientVersion.Major, clientVersion.Minor);

            // FullVersion can carry a suffix such as "7.2.8-nightly-20260101-0100" or "7.2.7+commit"
            var numericPart = serverFullVersion.Split('-', '+')[0];

            if (!Version.TryParse(numericPart, out var serverVersion))
            {
                error = $"ServiceControl expects RavenDB Server version {clientMajorMinor} or higher, but could not read the server version '{serverFullVersion}'.";
                return false;
            }

            // RavenDB ignores the patch for compatibility: https://docs.ravendb.net/7.2/client-api/faq/backward-compatibility
            if (new Version(serverVersion.Major, serverVersion.Minor) < clientMajorMinor)
            {
                error = $"ServiceControl expects RavenDB Server version {clientMajorMinor} or higher, but the server is using {serverVersion}. " +
                        "Upgrade the external RavenDB server or the servicecontrol-ravendb container before starting ServiceControl.";
                return false;
            }

            // Compacting a migrated database corrupts its attachments index before 7.2.5: https://github.com/ravendb/ravendb/releases/tag/7.2.5
            if (serverVersion.Major == 7 && serverVersion.Minor == 2 && serverVersion < FirstServerWithAttachmentsIndexFix)
            {
                error = $"RavenDB Server {serverVersion} can corrupt the attachments index of migrated databases during compaction. " +
                        $"Upgrade the external RavenDB server or the servicecontrol-ravendb container to {FirstServerWithAttachmentsIndexFix} or later before starting ServiceControl.";
                return false;
            }

            error = null;
            return true;
        }

        static readonly Version FirstServerWithAttachmentsIndexFix = new(7, 2, 5);

        static readonly Version ClientVersion = ParseClientVersion();

        static Version ParseClientVersion()
        {
            var clientVersion = typeof(Raven.Client.Constants).Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion;
            return Version.Parse(clientVersion.Split('-', '+')[0]);
        }

        static readonly ILogger Logger = LoggerUtil.CreateStaticLogger(typeof(StartupChecks));
    }
}
