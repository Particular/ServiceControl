#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ServiceControl.Persistence.DataMigration;

// Archive and unarchive operations share an id prefix with their batches. Completed retry history is a separate document and does not count.
sealed class UnfinishedOperationsCheck(RavenReadOnlySourceLifecycle lifecycle) : IMigrationStartupCheck
{
    public string Name => "RavenDB holds no unfinished retry, archive or unarchive operations";

    public async Task Run(CancellationToken cancellationToken = default)
    {
        var unfinished = new List<string>();

        using var session = lifecycle.OpenSession(lifecycle.Settings.DatabaseName);

        foreach (var (description, prefix) in Prefixes)
        {
            await using var documents = await session.Advanced.StreamAsync<JObject>(prefix, pageSize: 1, token: cancellationToken);

            if (await documents.MoveNextAsync())
            {
                unfinished.Add($"{description} (for example '{documents.Current.Id}')");
            }
        }

        if (unfinished.Count > 0)
        {
            throw new InvalidOperationException(
                $"The RavenDB database '{lifecycle.Settings.DatabaseName}' holds unfinished operations: {string.Join("; ", unfinished)}. The migration does not copy them. Start ServiceControl on RavenDB again, wait until its retry, archive and unarchive operations have finished, stop it, and run --start-data-migration again. If an archive or unarchive operation does not resume, request the same group action again on RavenDB. Stopping ServiceControl does not finish them.");
        }
    }

    static readonly (string Description, string Prefix)[] Prefixes =
    [
        ("retry batches and the forwarding marker", "RetryBatches/"),
        ("archive operations and their batches", "ArchiveOperations/"),
        ("unarchive operations and their batches", "UnarchiveOperations/")
    ];
}
