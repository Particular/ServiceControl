#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.Throughput;
using ServiceControl.Persistence.RavenDB.Throughput.Models;

/// <summary>
/// Reads the report masks document, whose list of masked strings becomes one Settings row. A read gives one batch
/// holding the list, or no rows when the document does not exist, and a list stored as null reads as an empty one.
/// The batch's cursor is the document's id, and a read resumed after that id gives nothing.
/// </summary>
sealed class LicensingReportMasksReader(RavenReadOnlySourceLifecycle lifecycle) : MigrationCategoryReader<List<string>>(lifecycle)
{
    const string DocumentId = LicensingDataStore.ReportMasksDocumentId;

    public override string CategoryId => MigrationCategoryIds.LicensingReportMasks;

    public override IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        ReadReportMasks(resumeAfter, cancellationToken);

    async IAsyncEnumerable<MigrationBatch> ReadReportMasks(string? resumeAfter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (resumeAfter == DocumentId)
        {
            yield break;
        }

        if (resumeAfter is not null)
        {
            throw new InvalidOperationException($"The migration cannot resume the '{CategoryId}' category after '{resumeAfter}', because its only cursor is '{DocumentId}'. Run --migration-retry {CategoryId} to copy the category again from the start.");
        }

        using var session = Lifecycle.OpenSession(Lifecycle.Settings.ThroughputDatabaseName);
        var document = await session.LoadAsync<ReportConfigurationDocument>(DocumentId, cancellationToken);

        yield return new MigrationBatch(
            document is null ? [] : [new MigrationRow(DocumentId, document.MaskedStrings ?? [], ReadOnlyDictionary<string, object?>.Empty)],
            DocumentId);
    }
}
