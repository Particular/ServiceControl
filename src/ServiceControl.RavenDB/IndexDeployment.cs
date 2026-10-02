namespace ServiceControl.RavenDB
{
    using System.Reflection;
    using System.Threading;
    using Raven.Client;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Indexes;

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

            // Operators can switch individual indexes from Corax to Lucene in RavenDB Studio, which stores the search engine
            // in the index configuration. Our definitions don't carry it, so deploying them as-is would make RavenDB build a
            // side-by-side replacement that falls back to the database default, which is Corax for databases created before
            // Lucene became the default. Carry the existing choice over so the definitions match and nothing gets rebuilt.
            var existingDefinitions = await store.Maintenance.SendAsync(new GetIndexesOperation(0, int.MaxValue), cancellationToken);
            var existingByName = existingDefinitions.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var index in indexList)
            {
                // A pending replacement reflects the latest change made by the operator, so it takes precedence
                if (TryGetSearchEngineType(existingByName, Constants.Documents.Indexing.SideBySideIndexNamePrefix + index.IndexName, out var searchEngineType)
                    || TryGetSearchEngineType(existingByName, index.IndexName, out searchEngineType))
                {
                    index.Configuration[StaticSearchEngineTypeKey] = searchEngineType;
                }
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
    }
}
