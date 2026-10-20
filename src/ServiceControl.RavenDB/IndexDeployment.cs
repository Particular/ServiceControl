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
            var databaseDefaultIsLucene = string.Equals(databaseDefault, nameof(SearchEngineType.Lucene), StringComparison.OrdinalIgnoreCase);

            Logger.LogInformation("Database {DatabaseName} static index search engine setting: {DatabaseDefault}", store.Database, databaseDefault ?? "not set, the server default applies");

            foreach (var index in indexList)
            {
                var replacementName = Constants.Documents.Indexing.SideBySideIndexNamePrefix + index.IndexName;
                existingByName.TryGetValue(index.IndexName, out var existing);
                existingByName.TryGetValue(replacementName, out var pendingReplacement);

                var changes = existing == null ? IndexDefinitionCompareDifferences.None : RebuildChanges(existing, index, store);
                var changed = changes != IndexDefinitionCompareDifferences.None;

                if (existing?.LockMode is IndexLockMode.LockedIgnore or IndexLockMode.LockedError)
                {
                    if (changed)
                    {
                        Logger.LogError("Index {IndexName} is locked ({LockMode}) and its definition changed ({Changes}). The change is not applied until the index is unlocked", index.IndexName, existing.LockMode, changes);
                    }
                    else
                    {
                        Logger.LogWarning("Index {IndexName} is locked ({LockMode}). Definition changes from new versions are not applied until the index is unlocked", index.IndexName, existing.LockMode);
                    }
                }

                // 1. A search engine set in our index definition always wins. It can be set through SearchEngineType or Configuration.
                if (index.SearchEngineType.HasValue)
                {
                    Logger.LogInformation("Index {IndexName} uses the {SearchEngineType} search engine set in its definition", index.IndexName, index.SearchEngineType);
                    continue;
                }

                if (index.Configuration.TryGetValue(StaticSearchEngineTypeKey, out var definedSearchEngineType) && !string.IsNullOrEmpty(definedSearchEngineType))
                {
                    Logger.LogInformation("Index {IndexName} uses the {SearchEngineType} search engine set in its definition", index.IndexName, definedSearchEngineType);
                    continue;
                }

                // 2. Keep a search engine set on the index on the server, for example by an operator in RavenDB Studio. The
                // definitions then match and RavenDB does not rebuild the index. A pending replacement is checked first:
                // when the operator changes the search engine, the replacement holds the latest choice. A replacement
                // without a search engine, created by 6.20.0 and 6.21.0 at start-up, is discarded by RavenDB once the
                // definition matches the original index again.
                if (TryGetSearchEngineType(pendingReplacement, out var searchEngineType) || TryGetSearchEngineType(existing, out searchEngineType))
                {
                    index.Configuration[StaticSearchEngineTypeKey] = searchEngineType;
                    Logger.LogInformation("Index {IndexName} keeps the {SearchEngineType} search engine set on the index", index.IndexName, searchEngineType);
                    continue;
                }

                // 3. A new index, or an index that RavenDB rebuilds because the definition changed, uses Lucene, the
                // application default. It inherits Lucene when the database is set to Lucene. Otherwise Lucene is set on
                // the index, so the index does not follow a database default of Corax.
                if (existing == null || changed)
                {
                    var reason = existing == null ? "is created" : $"is rebuilt because its definition changed ({changes})";

                    // A rebuild of a large index takes long and degrades ingestion, see docs.particular.net/servicecontrol/upgrades/#upgrades-that-rebuild-indexes
                    var level = changed ? LogLevel.Warning : LogLevel.Information;

                    if (databaseDefaultIsLucene)
                    {
                        Logger.Log(level, "Index {IndexName} {Reason} and inherits the Lucene search engine from the database", index.IndexName, reason);
                    }
                    else
                    {
                        index.Configuration[StaticSearchEngineTypeKey] = nameof(SearchEngineType.Lucene);
                        Logger.Log(level, "Index {IndexName} {Reason} with the Lucene search engine set on the index, the database setting is {DatabaseDefault}", index.IndexName, reason, databaseDefault ?? "not set");
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
        const IndexDefinitionCompareDifferences RebuildDifferences = IndexDefinitionCompareDifferences.Maps
            | IndexDefinitionCompareDifferences.Reduce
            | IndexDefinitionCompareDifferences.Fields
            | IndexDefinitionCompareDifferences.AdditionalSources
            | IndexDefinitionCompareDifferences.AdditionalAssemblies;

        static IndexDefinitionCompareDifferences RebuildChanges(IndexDefinition existingDefinition, AbstractIndexCreationTask index, IDocumentStore store)
        {
            index.Conventions ??= store.Conventions;

            return existingDefinition.Compare(index.CreateIndexDefinition()) & RebuildDifferences;
        }

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
