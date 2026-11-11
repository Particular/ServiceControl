#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Raven.Client;
using Raven.Client.Documents.Commands;
using Raven.Client.Documents.Conventions;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.Throughput;
using ServiceControl.Persistence.RavenDB.Throughput.Models;

/// <summary>
/// What the throughput database's readers share: which documents are endpoints, and how an endpoint document becomes a row.
/// </summary>
static class ThroughputDocuments
{
    static readonly string EndpointCollection = DocumentConventions.DefaultGetCollectionName(typeof(EndpointDocument));

    /// <summary>
    /// Reads the endpoints in ordinal document-id order, after <paramref name="resumeAfter" />, in batches of at most
    /// <paramref name="batchSize" /> rows, each with its cursor on the last endpoint's id. Throws
    /// <see cref="InvalidOperationException" /> when the source holds no document with the id in <paramref name="resumeAfter" />.
    /// </summary>
    public static async IAsyncEnumerable<MigrationBatch> Endpoints(
        RavenReadOnlySourceLifecycle lifecycle,
        string categoryId,
        string? resumeAfter,
        int batchSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var databaseName = lifecycle.Settings.ThroughputDatabaseName;
        var endpoints = new List<MigrationRow>();

        using (var session = lifecycle.OpenSession(databaseName))
        {
            await RavenDocumentStream.RefuseMissingCursor(session, categoryId, databaseName, resumeAfter, cancellationToken);

            // With no id prefix RavenDB streams in reverse etag order and ignores startAfter, so the order and the cursor are applied here.
            await using var stream = await session.Advanced.StreamAsync<EndpointDocument>(string.Empty, token: cancellationToken);

            while (await stream.MoveNextAsync())
            {
                if (Endpoint(stream.Current) is { } row && (resumeAfter is null || string.CompareOrdinal(row.SourceId, resumeAfter) > 0))
                {
                    endpoints.Add(row);
                }
            }
        }

        endpoints.Sort((left, right) => string.CompareOrdinal(left.SourceId, right.SourceId));

        foreach (var chunk in endpoints.Chunk(batchSize))
        {
            yield return new MigrationBatch(chunk, chunk[^1].SourceId);
        }
    }

    /// <summary>
    /// Turns one streamed document into a row that carries an <see cref="Particular.LicensingComponent.Contracts.Endpoint" />. Returns null when the document is not in the EndpointDocuments collection, which is the only place the product reads endpoints from. 
    /// The throughput database gives its documents no id prefix, so the stream also hands back its singleton documents and anything else stored there.
    /// </summary>
    public static MigrationRow? Endpoint(StreamResult<EndpointDocument> entry) =>
        IsEndpoint(entry) ? new MigrationRow(entry.Id, entry.Document.ToEndpoint(), ReadOnlyDictionary<string, object?>.Empty) : null;

    // RavenDB matches collection names ignoring case.
    static bool IsEndpoint(StreamResult<EndpointDocument> entry) =>
        entry.Metadata.TryGetValue(Constants.Documents.Metadata.Collection, out var collection) && string.Equals(collection?.ToString(), EndpointCollection, StringComparison.OrdinalIgnoreCase);
}
