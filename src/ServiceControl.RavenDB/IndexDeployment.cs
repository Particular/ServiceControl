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

            // Our index definitions do not set a search engine. RavenDB then uses the database default for them,
            // which, starting in 6.20, ServiceControl sets to Lucene when creating new databases; existing
            // databases are left untouched. 
            // An operator can set a different search engine on one index in RavenDB Studio, for example Lucene instead of
            // Corax, as the migration guide recommends. RavenDB stores that choice in the configuration of that index only.
            // RavenDB compares the configuration as part of the index definition. At the next start-up, our definition has
            // no search engine and does not match the definition on the server. RavenDB then builds a side-by-side
            // replacement with the database default. For databases created before Lucene became the default, that default
            // is Corax. This undoes the migration and starts a full rebuild of the index. On a large database, the rebuild
            // can take days. To prevent this, the search engine of each index is resolved before deployment:
            var existingDefinitions = await store.Maintenance.SendAsync(new GetIndexesOperation(0, int.MaxValue), cancellationToken);
            var existingByName = existingDefinitions.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var index in indexList)
            {
                // 1. A search engine set in our index definition always wins. It can be set through SearchEngineType or Configuration.
                if (index.SearchEngineType.HasValue
                    || (index.Configuration.TryGetValue(StaticSearchEngineTypeKey, out var configuredSearchEngineType) && !string.IsNullOrEmpty(configuredSearchEngineType)))
                {
                    continue;
                }

                var replacementName = Constants.Documents.Indexing.SideBySideIndexNamePrefix + index.IndexName;

                // 2. Keep the search engine set on the index on the server. The definitions then match and RavenDB does not
                // rebuild the index. A pending replacement is checked first. When the operator changes the search engine,
                // the replacement holds the latest choice. ServiceControl 6.20.0 and 6.21.0 also created replacements when
                // they reset a migrated index at start-up. Those replacements have no search engine. The search engine of
                // the original index is then used. The definition matches the original index again and RavenDB discards
                // the replacement.
                if (TryGetSearchEngineType(existingByName, replacementName, out var searchEngineType)
                    || TryGetSearchEngineType(existingByName, index.IndexName, out searchEngineType))
                {
                    index.Configuration[StaticSearchEngineTypeKey] = searchEngineType;
                    Logger.LogInformation("Index {IndexName} keeps the configured {SearchEngineType} search engine", index.IndexName, searchEngineType);
                }
                // 3. An index that does not exist yet is created with Lucene. Lucene performs better for our workload, also
                // in databases that still default to Corax. The search engine is set on the index itself. The index then
                // stays on Lucene when the database default changes.
                else if (!existingByName.ContainsKey(index.IndexName) && !existingByName.ContainsKey(replacementName))
                {
                    index.Configuration[StaticSearchEngineTypeKey] = nameof(SearchEngineType.Lucene);
                    Logger.LogInformation("Index {IndexName} is created with the Lucene search engine", index.IndexName);
                }

                // 4. An existing index without a search engine of its own continues to use the database default. A search
                // engine set now changes the definition and starts the full rebuild that this code prevents.
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
