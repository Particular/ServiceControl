namespace TestHelper;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.Exceptions.Database;
using Raven.Embedded;
using ServiceControl.RavenDB;

// A dynamic query makes RavenDB create an auto index. An auto index has no per-index search engine setting, so the search
// engine custom check keeps reporting it after all static indexes use Lucene. Query through a static index instead.
public static class AutoIndexes
{
    public static async Task<string[]> FindAutoIndexes(this IDocumentStore store, string databaseName, CancellationToken cancellationToken = default)
    {
        try
        {
            var indexNames = await store.Maintenance.ForDatabase(databaseName).SendAsync(new GetIndexNamesOperation(0, int.MaxValue), cancellationToken);

            return [.. indexNames
                .Where(name => name.StartsWith("Auto/", StringComparison.OrdinalIgnoreCase))
                .Select(name => $"{databaseName}: {name}")];
        }
        catch (DatabaseDoesNotExistException)
        {
            return [];
        }
    }

    public static async Task<string[]> FindAutoIndexes(this EmbeddedDatabase database, IEnumerable<string> databaseNames, CancellationToken cancellationToken = default)
    {
        List<string> autoIndexes = [];

        foreach (var databaseName in databaseNames)
        {
            using var store = await EmbeddedServer.Instance.GetDocumentStoreAsync(new DatabaseOptions(databaseName) { SkipCreatingDatabase = true }, cancellationToken);
            autoIndexes.AddRange(await store.FindAutoIndexes(databaseName, cancellationToken));
        }

        return [.. autoIndexes];
    }

    public static void AssertNone(IEnumerable<string> autoIndexes) =>
        Assert.That(autoIndexes, Is.Empty, "A dynamic query created an auto index. Query through a static index instead");
}
