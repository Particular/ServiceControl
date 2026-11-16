#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.Throughput;

/// <summary>
/// Reads the licensed endpoint details document, which becomes one Settings row. A read gives one batch holding the
/// whole document, or no rows when the document does not exist. The batch's cursor is the document's id, and a read
/// resumed after that id gives nothing.
/// </summary>
sealed class LicensedEndpointDetailsReader(RavenReadOnlySourceLifecycle lifecycle)
    : SingleDocumentReader<LicensedEndpointDetails, LicensedEndpointDetails>(lifecycle, lifecycle.Settings.ThroughputDatabaseName, LicensingDataStore.LicencedEndpointDetailsDocumentId)
{
    public override string CategoryId => MigrationCategoryIds.LicensedEndpointDetails;

    protected override IEnumerable<(string SourceId, LicensedEndpointDetails Document)> RowsFrom(LicensedEndpointDetails document) =>
        [(DocumentId, document)];
}
