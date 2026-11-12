namespace ServiceControl.Persistence.EFCore.DataMigration;

using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation;

/// <summary>
/// Turns a licensing endpoint read from the source into the row the target stores, with the same key and the same
/// stand-ins for missing values as the product's own save.
/// </summary>
static class LicensingEndpointRowMapper
{
    public static LicensingEndpointEntity Map(Endpoint endpoint)
    {
        var sanitizedName = endpoint.SanitizedName ?? string.Empty;

        return new LicensingEndpointEntity
        {
            NormalizedName = LicensingDataStore.Normalize(endpoint.Id.Name),
            ThroughputSource = endpoint.Id.ThroughputSource,
            Name = endpoint.Id.Name,
            SanitizedName = sanitizedName,
            NormalizedSanitizedName = LicensingDataStore.Normalize(sanitizedName),
            UserIndicator = endpoint.UserIndicator ?? string.Empty,
            Scope = endpoint.Scope,
            EndpointIndicators = endpoint.EndpointIndicators?.ToList() ?? []
        };
    }
}
