#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using System.Threading;
using Raven.Client;
using Raven.Client.Documents.Commands;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads the messages ServiceControl took off the error queue but could not store, each with its body and the time it failed.
/// </summary>
sealed class FailedErrorImportsReader(RavenReadOnlySourceLifecycle lifecycle) : MigrationCategoryReader<FailedErrorImport>(lifecycle)
{
    public override string CategoryId => MigrationCategoryIds.FailedErrorImports;

    public override IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        Documents($"{FailedErrorImportDataStore.CollectionName}/", resumeAfter, batchSize, ToRow, cancellationToken);

    // A document with no message still gets an empty body, because the engine would otherwise ask ReadBody, which refuses this category.
    static MigrationRow ToRow(StreamResult<FailedErrorImport> result) => new(
        result.Id,
        result.Document,
        new Dictionary<string, object?> { [MigrationMetadataKeys.FailedAt] = FailedMessageStream.ReadMetadataDate(result, Constants.Documents.Metadata.LastModified) },
        new MigrationBody(
            result.Document.Message?.Body ?? [],
            result.Document.Message?.Headers?.GetValueOrDefault(NServiceBus.Headers.ContentType) ?? "application/octet-stream"));
}
