#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.Throughput;
using ServiceControl.Persistence.RavenDB.Throughput.Models;

/// <summary>
/// Reads the report masks document, whose list of masked strings becomes one Settings row. A read gives one batch
/// holding the list, or no rows when the document does not exist, and a list stored as null reads as an empty one.
/// The batch's cursor is the document's id, and a read resumed after that id gives nothing.
/// </summary>
sealed class LicensingReportMasksReader(RavenReadOnlySourceLifecycle lifecycle)
    : SingleDocumentReader<ReportConfigurationDocument, List<string>>(lifecycle, lifecycle.Settings.ThroughputDatabaseName, LicensingDataStore.ReportMasksDocumentId)
{
    public override string CategoryId => MigrationCategoryIds.LicensingReportMasks;

    protected override IEnumerable<(string SourceId, List<string> Document)> RowsFrom(ReportConfigurationDocument document) =>
        [(DocumentId, document.MaskedStrings ?? [])];
}
