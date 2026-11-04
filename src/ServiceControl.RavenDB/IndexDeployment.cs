namespace ServiceControl.RavenDB
{
    using System.Reflection;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using Raven.Client;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Indexes;
    using Raven.Client.ServerWide.Operations;
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

            // RavenDB compares the configuration as part of the index definition. Our definitions set no search engine,
            // so the engine has to be resolved before deployment. Otherwise RavenDB builds a side-by-side replacement
            // with the database default, which can undo a migration done by an operator in RavenDB Studio.
            var existingDefinitions = await store.Maintenance.SendAsync(new GetIndexesOperation(0, int.MaxValue), cancellationToken);
            var existingByName = existingDefinitions.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

            var databaseDefault = await GetDatabaseSearchEngineType(store, cancellationToken);
            var databaseDefaultIsLucene = IsLucene(databaseDefault);

            foreach (var index in indexList)
            {
                var replacementName = Constants.Documents.Indexing.SideBySideIndexNamePrefix + index.IndexName;
                existingByName.TryGetValue(index.IndexName, out var existingDefinition);
                existingByName.TryGetValue(replacementName, out var pendingReplacementDefinition);

                // 1. A search engine set in our index definition always wins. It can be set through SearchEngineType or Configuration.
                var definedSearchEngineType = GetDefinedSearchEngineType(index);
                if (definedSearchEngineType != null)
                {
                    if (IsLucene(definedSearchEngineType))
                    {
                        Logger.LogInformation("Index {IndexName} uses the Lucene search engine set in its definition", index.IndexName);
                    }
                    else
                    {
                        Logger.LogWarning("Index {IndexName} uses the {SearchEngineType} search engine set in its definition. ServiceControl supports only Lucene", index.IndexName, definedSearchEngineType);
                    }

                    continue;
                }

                var rebuildTriggers = existingDefinition == null ? IndexDefinitionCompareDifferences.None : GetRebuildTriggeringDifferences(existingDefinition, index, store);
                var requiresRebuild = rebuildTriggers != IndexDefinitionCompareDifferences.None;

                // 2. Keep Lucene set on the index on the server, for example by an operator in RavenDB Studio. The
                // definitions then match and RavenDB does not rebuild the index. A pending replacement is checked first:
                // when the operator changes the search engine, the replacement holds the latest choice. A replacement
                // without a search engine, created by 6.20.0 and 6.21.0 at start-up, is discarded by RavenDB once the
                // definition matches the original index again.
                if (HasSearchEngineTypeSetOnIndex(pendingReplacementDefinition, IsLucene) || HasSearchEngineTypeSetOnIndex(existingDefinition, IsLucene))
                {
                    index.Configuration[StaticSearchEngineTypeKey] = nameof(SearchEngineType.Lucene);
                    Logger.LogInformation("Index {IndexName} keeps the Lucene search engine set on the index", index.IndexName);
                    continue;
                }

                // 2. Another search engine set on an unchanged index is kept, because dropping the setting makes RavenDB
                // rebuild the index. It is replaced by Lucene when the index is rebuilt anyway (rule 3).
                if (!requiresRebuild && TryGetSearchEngineType(existingDefinition, out var unsupportedSearchEngineType))
                {
                    index.Configuration[StaticSearchEngineTypeKey] = unsupportedSearchEngineType;
                    Logger.LogWarning("Index {IndexName} has the {SearchEngineType} search engine set on the index. ServiceControl supports only Lucene and sets Lucene when the index is rebuilt", index.IndexName, unsupportedSearchEngineType);
                    continue;
                }

                // 3. A new index, or an index that RavenDB rebuilds because the definition changed, uses Lucene. It inherits
                // Lucene when the database is set to Lucene. Otherwise Lucene is set on the index, so the index does not
                // follow a database default of Corax.
                if (existingDefinition == null || requiresRebuild)
                {
                    var reason = existingDefinition == null ? "is created" : $"is rebuilt because its definition changed ({rebuildTriggers})";

                    if (databaseDefaultIsLucene)
                    {
                        Logger.LogInformation("Index {IndexName} {Reason} and inherits the Lucene search engine from the database", index.IndexName, reason);
                    }
                    else
                    {
                        index.Configuration[StaticSearchEngineTypeKey] = nameof(SearchEngineType.Lucene);
                        Logger.LogInformation("Index {IndexName} {Reason} with the Lucene search engine set on the index, the database setting is {DatabaseDefault}", index.IndexName, reason, databaseDefault ?? "not set");
                    }

                    continue;
                }

                // 4. An unchanged index without a search engine of its own is left alone. RavenDB treats a search engine added
                // to the index configuration as a change and rebuilds the index, also when the engine is the same.
                Logger.LogInformation("Index {IndexName} is unchanged and inherits the search engine from the database", index.IndexName);
            }

            await IndexCreation.CreateIndexesAsync(indexList, store, null, null, cancellationToken);
        }

        static async Task<string> GetDatabaseSearchEngineType(IDocumentStore store, CancellationToken cancellationToken)
        {
            var record = await store.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(store.Database), cancellationToken);

            return record?.Settings != null && record.Settings.TryGetValue(StaticSearchEngineTypeKey, out var value) && !string.IsNullOrEmpty(value)
                ? value
                : null;
        }

        // The differences that make RavenDB build a side-by-side replacement.
        const IndexDefinitionCompareDifferences RebuildTriggeringDifferences = IndexDefinitionCompareDifferences.Maps
            | IndexDefinitionCompareDifferences.Reduce
            | IndexDefinitionCompareDifferences.Fields
            | IndexDefinitionCompareDifferences.AdditionalSources
            | IndexDefinitionCompareDifferences.AdditionalAssemblies;

        static IndexDefinitionCompareDifferences GetRebuildTriggeringDifferences(IndexDefinition existingDefinition, AbstractIndexCreationTask index, IDocumentStore store)
        {
            index.Conventions ??= store.Conventions;

            return existingDefinition.Compare(index.CreateIndexDefinition()) & RebuildTriggeringDifferences;
        }

        static string GetDefinedSearchEngineType(AbstractIndexCreationTask index)
        {
            if (index.SearchEngineType.HasValue)
            {
                return index.SearchEngineType.Value.ToString();
            }

            return index.Configuration.TryGetValue(StaticSearchEngineTypeKey, out var configured) && !string.IsNullOrEmpty(configured) ? configured : null;
        }

        static bool HasSearchEngineTypeSetOnIndex(IndexDefinition definition, Func<string, bool> predicate) =>
            TryGetSearchEngineType(definition, out var searchEngineType) && predicate(searchEngineType);

        static bool IsLucene(string searchEngineType) => string.Equals(searchEngineType, nameof(SearchEngineType.Lucene), StringComparison.OrdinalIgnoreCase);

        static bool TryGetSearchEngineType(IndexDefinition definition, out string searchEngineType)
        {
            searchEngineType = null;

            return definition?.Configuration != null
                && definition.Configuration.TryGetValue(StaticSearchEngineTypeKey, out searchEngineType)
                && !string.IsNullOrEmpty(searchEngineType);
        }

        public const string StaticSearchEngineTypeKey = "Indexing.Static.SearchEngineType";

        static readonly ILogger Logger = LoggerUtil.CreateStaticLogger(typeof(IndexDeployment));
    }
}
