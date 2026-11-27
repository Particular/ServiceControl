#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using ServiceControl.Contracts.CustomChecks;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads the custom check results out of the primary database, each one turned into the shared <see cref="CustomCheckDetail" />, a type the target can reference.
/// A null value in the stored document stays null, so the writer can name the column it would fill.
/// A result of one of the RavenDB persister's own checks carries the SourceStorageCheck metadata value, so the target can skip it.
/// </summary>
sealed class CustomChecksReader(RavenReadOnlySourceLifecycle lifecycle) : MigrationCategoryReader<CustomCheckDetail>(lifecycle)
{
    public override string CategoryId => MigrationCategoryIds.CustomChecks;

    public override IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        RavenDocumentStream.ByPrefix<CustomCheck>(Lifecycle, CategoryId, Lifecycle.Settings.DatabaseName, $"{RavenCustomCheckDataStore.CollectionName}/", resumeAfter, batchSize, RavenDocumentStream.MaxDocumentsWalkedPerBatch,
            static result => new MigrationRow(result.Id, ToDetail(result.Document), MetadataFor(result.Document)), cancellationToken);

    static IReadOnlyDictionary<string, object?> MetadataFor(CustomCheck document) =>
        document.CustomCheckId is { } id && document.Category is { } category && RavenStorageCheckIds.All.Contains((id, category))
            ? new Dictionary<string, object?> { [MigrationMetadataKeys.SourceStorageCheck] = true }
            : ReadOnlyDictionary<string, object?>.Empty;

    static CustomCheckDetail ToDetail(CustomCheck document) => new()
    {
        OriginatingEndpoint = document.OriginatingEndpoint!,
        CustomCheckId = document.CustomCheckId!,
        ReportedAt = document.ReportedAt,
        Category = document.Category!,
        HasFailed = document.Status == Status.Fail,
        FailureReason = document.FailureReason
    };
}
