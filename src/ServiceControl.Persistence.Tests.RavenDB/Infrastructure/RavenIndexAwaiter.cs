using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.Indexes;

public static class RavenIndexAwaiter
{
    // CI runners can be slow. Three new indexes on a fresh database have been seen to take more than 10 seconds for
    // their first indexing pass on a Windows runner. The wait returns as soon as the indexes are up to date, so a
    // large budget only costs time when something is actually wrong.
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
    static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public static Task WaitForIndexingAsync(this IDocumentStore store, CancellationToken cancellationToken = default) =>
        store.WaitForIndexingAsync(DefaultTimeout, cancellationToken);

    public static async Task WaitForIndexingAsync(this IDocumentStore store, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        IndexStats[] stats;

        do
        {
            stats = await store.Maintenance.SendAsync(new GetIndexesStatisticsOperation(), cancellationToken);

            // An index in the error state never becomes up to date. Fail now with the errors instead of after the timeout.
            var erroredIndexes = stats.Where(i => i.State == IndexState.Error || i.ErrorsCount > 0).Select(i => i.Name).ToArray();
            if (erroredIndexes.Length > 0)
            {
                var errors = await store.Maintenance.SendAsync(new GetIndexErrorsOperation(erroredIndexes), cancellationToken);
                var details = errors.SelectMany(e => e.Errors.Select(x => $"{e.Name}: {x.Action} {x.Document} {x.Error}"));
                Assert.Fail($"Indexes have errors:{Environment.NewLine}{string.Join(Environment.NewLine, details)}");
            }

            if (stats.All(i => !i.IsStale))
            {
                return;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
        while (stopwatch.Elapsed < timeout);

        var stale = stats.Where(i => i.IsStale).Select(i => $"{i.Name} (state: {i.State}, status: {i.Status}, entries: {i.EntriesCount})");
        Assert.Fail($"Indexes were still stale after {timeout}:{Environment.NewLine}{string.Join(Environment.NewLine, stale)}");
    }
}
