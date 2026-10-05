namespace ServiceControl.RavenDB
{
    using System.Reflection;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using Raven.Client;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Indexes;
    using ServiceControl.Infrastructure;

    public static class IndexDeployment
    {
        public static Task CreateIndexesAsync(Assembly assembly, IDocumentStore store, CancellationToken cancellationToken = default)
        {
            var indexes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(AbstractIndexCreationTask)))
                .Select(t => (AbstractIndexCreationTask)Activator.CreateInstance(t));

            return CreateIndexesAsync(indexes, store, cancellationToken);
        }

        public static async Task CreateIndexesAsync(IEnumerable<AbstractIndexCreationTask> indexes, IDocumentStore store, CancellationToken cancellationToken = default)
        {
            var indexList = indexes.ToList();

            // Our index definitions don't set a search engine, so RavenDB uses the database default for them. Operators can
            // switch an individual index to another search engine in RavenDB Studio (e.g. from Corax to Lucene, as the
            // migration guide recommends), which stores the choice in the configuration of that index only. RavenDB treats
            // the configuration as part of the index definition, so at the next start-up our definition, without a search
            // engine, would no longer match the one on the server. RavenDB would then build a side-by-side replacement using
            // the database default, which is Corax for databases created before Lucene became the default, silently undoing
            // the migration and triggering a full rebuild of the index that can take days on large databases.
            // To prevent that, the search engine of each index is resolved before deploying it:
            var existingDefinitions = await store.Maintenance.SendAsync(new GetIndexesOperation(0, int.MaxValue), cancellationToken);
            var existingByName = existingDefinitions.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var index in indexList)
            {
                // 1. A search engine set explicitly in our index definition, through SearchEngineType or Configuration, always wins
                if (index.SearchEngineType.HasValue
                    || (index.Configuration.TryGetValue(StaticSearchEngineTypeKey, out var configuredSearchEngineType) && !string.IsNullOrEmpty(configuredSearchEngineType)))
                {
                    continue;
                }

                var replacementName = Constants.Documents.Indexing.SideBySideIndexNamePrefix + index.IndexName;

                // 2. Otherwise keep the search engine set on the index on the server, so the definitions match and nothing
                // gets rebuilt. A pending replacement is checked first: it can be the operator switching the search engine,
                // in which case its configuration is the latest choice. It can also have been created by ServiceControl
                // 6.20.0 or 6.21.0 resetting a migrated index at start-up; that replacement has no search engine set, so the
                // one on the original index is used and RavenDB discards the replacement as the definition matches again.
                if (TryGetSearchEngineType(existingByName, replacementName, out var searchEngineType)
                    || TryGetSearchEngineType(existingByName, index.IndexName, out searchEngineType))
                {
                    index.Configuration[StaticSearchEngineTypeKey] = searchEngineType;
                    Logger.LogInformation("Keeping the {SearchEngineType} search engine configured on index {IndexName}", searchEngineType, index.IndexName);
                }
                // 3. Indexes that don't exist yet are created with Lucene, which performs better for our workload, also in
                // existing databases that still default to Corax. Pinning it on the index keeps it on Lucene even if the
                // database default changes later.
                else if (!existingByName.ContainsKey(index.IndexName) && !existingByName.ContainsKey(replacementName))
                {
                    index.Configuration[StaticSearchEngineTypeKey] = nameof(SearchEngineType.Lucene);
                    Logger.LogInformation("Creating index {IndexName} with the Lucene search engine", index.IndexName);
                }

                // 4. Existing indexes without a search engine of their own keep inheriting the database default. Setting
                // one now would change their definition and trigger the full rebuild this is meant to avoid.
            }

            await IndexCreation.CreateIndexesAsync(indexList, store, null, null, cancellationToken);
        }

        static bool TryGetSearchEngineType(Dictionary<string, IndexDefinition> definitions, string indexName, out string searchEngineType)
        {
            searchEngineType = null;

            return definitions.TryGetValue(indexName, out var definition)
                && definition.Configuration != null
                && definition.Configuration.TryGetValue(StaticSearchEngineTypeKey, out searchEngineType)
                && !string.IsNullOrEmpty(searchEngineType);
        }

        public const string StaticSearchEngineTypeKey = "Indexing.Static.SearchEngineType";

        static readonly ILogger Logger = LoggerUtil.CreateStaticLogger(typeof(IndexDeployment));
    }
}
