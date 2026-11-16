#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using System.Linq;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.Recoverability;
using ServiceControl.Recoverability;

/// <summary>
/// Reads the retry history document as rows, historic operations first, then unacknowledged ones.
/// </summary>
sealed class RetryOperationsReader(RavenReadOnlySourceLifecycle lifecycle)
    : SingleDocumentReader<RetryHistory, RetryOperationRow>(lifecycle, lifecycle.Settings.DatabaseName, RetryHistoryDataStore.DocumentId)
{
    public override string CategoryId => MigrationCategoryIds.RetryOperations;

    protected override IEnumerable<(string SourceId, RetryOperationRow Document)> RowsFrom(RetryHistory document) =>
    [
        .. document.HistoricOperations.Select(operation => ($"historic|{operation.RequestId}|{operation.RetryType}|{operation.CompletionTime:O}", new RetryOperationRow(operation, null))),
        .. document.UnacknowledgedOperations.Select(operation => ($"unacknowledged|{operation.RequestId}|{operation.RetryType}|{operation.CompletionTime:O}", new RetryOperationRow(null, operation)))
    ];
}
