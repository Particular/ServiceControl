namespace ServiceControl.Persistence.EFCore.DataMigration;

using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation;

/// <summary>
/// Turns one endpoint's daily count read from the source into the row the target stores, keyed by the same
/// normalised name the product's own licensing store computes.
/// </summary>
static class LicensingThroughputRowMapper
{
    public static LicensingEndpointThroughputEntity Map(ThroughputDayRow row)
    {
        return new LicensingEndpointThroughputEntity
        {
            NormalizedName = LicensingDataStore.Normalize(row.Endpoint.Name),
            ThroughputSource = row.Endpoint.ThroughputSource,
            DateUtc = row.DateUtc,
            MessageCount = row.MessageCount
        };
    }
}
