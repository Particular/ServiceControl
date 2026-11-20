#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads the trial metadata document, whose end date becomes one Settings row.
/// </summary>
sealed class TrialEndDateReader(RavenReadOnlySourceLifecycle lifecycle)
    : SingleDocumentReader<TrialMetadata, TrialMetadata>(lifecycle, lifecycle.Settings.DatabaseName, TrialLicenseDataProvider.TrialMetadataId)
{
    public override string CategoryId => MigrationCategoryIds.TrialEndDate;

    protected override IEnumerable<(string SourceId, TrialMetadata Document)> RowsFrom(TrialMetadata document) => [(DocumentId, document)];
}
