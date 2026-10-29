#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration;

using System;
using System.Collections.ObjectModel;
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
    /// Turns one streamed document into a row that carries an <see cref="Particular.LicensingComponent.Contracts.Endpoint" />. Returns null when the document is not in the EndpointDocuments collection, which is the only place the product reads endpoints from. 
    /// The throughput database gives its documents no id prefix, so the stream also hands back its singleton documents and anything else stored there.
    /// </summary>
    public static MigrationRow? Endpoint(StreamResult<EndpointDocument> entry) =>
        IsEndpoint(entry) ? new MigrationRow(entry.Id, entry.Document.ToEndpoint(), ReadOnlyDictionary<string, object?>.Empty) : null;

    // RavenDB matches collection names ignoring case.
    static bool IsEndpoint(StreamResult<EndpointDocument> entry) =>
        entry.Metadata.TryGetValue(Constants.Documents.Metadata.Collection, out var collection) && string.Equals(collection?.ToString(), EndpointCollection, StringComparison.OrdinalIgnoreCase);
}
