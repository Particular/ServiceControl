#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Particular.LicensingComponent.Contracts;
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
        var endpoints = (await AllEndpoints(lifecycle, categoryId, resumeAfter, cancellationToken))
            .Where(row => resumeAfter is null || string.CompareOrdinal(row.SourceId, resumeAfter) > 0)
            .OrderBy(row => row.SourceId, StringComparer.Ordinal);

        foreach (var chunk in endpoints.Chunk(batchSize))
        {
            yield return new MigrationBatch(chunk, chunk[^1].SourceId);
        }
    }

    /// <summary>
    /// Reads the endpoints in groups whose names can share a key in SQL: the same source, and the same name ignoring
    /// case and trailing spaces. Groups come in the ordinal order of their last document id, which is each group's cursor,
    /// and only groups after <paramref name="resumeAfter" /> are read. Throws <see cref="InvalidOperationException" /> when
    /// the source holds no document with the id in <paramref name="resumeAfter" />.
    /// </summary>
    public static async IAsyncEnumerable<IReadOnlyList<MigrationRow>> EndpointGroups(
        RavenReadOnlySourceLifecycle lifecycle,
        string categoryId,
        string? resumeAfter,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var groups = (await AllEndpoints(lifecycle, categoryId, resumeAfter, cancellationToken))
            .GroupBy(SharedKey)
            .Select(group => group.OrderBy(row => row.SourceId, StringComparer.Ordinal).ToList())
            .Where(group => resumeAfter is null || string.CompareOrdinal(group[^1].SourceId, resumeAfter) > 0)
            .OrderBy(group => group[^1].SourceId, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            yield return group;
        }
    }

    // SQL Server's default collation ignores case and trailing spaces, and the product lower-cases the name it stores.
    static (string? Name, ThroughputSource Source) SharedKey(MigrationRow row)
    {
        var id = ((Endpoint)row.Document).Id;
        return (id.Name?.TrimEnd(' ').ToLowerInvariant(), id.ThroughputSource);
    }

    // With no id prefix RavenDB streams in reverse etag order and ignores startAfter, so callers order the rows and apply the cursor.
    static async Task<List<MigrationRow>> AllEndpoints(RavenReadOnlySourceLifecycle lifecycle, string categoryId, string? resumeAfter, CancellationToken cancellationToken)
    {
        var databaseName = lifecycle.Settings.ThroughputDatabaseName;
        var endpoints = new List<MigrationRow>();

        using var session = lifecycle.OpenSession(databaseName);
        await RavenDocumentStream.RefuseMissingCursor(session, categoryId, databaseName, resumeAfter, cancellationToken);

        await using var stream = await session.Advanced.StreamAsync<EndpointDocument>(string.Empty, token: cancellationToken);

        while (await stream.MoveNextAsync())
        {
            if (Endpoint(stream.Current) is { } row)
            {
                endpoints.Add(row);
            }
        }

        return endpoints;
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
