namespace ServiceControl.Persistence.EFCore.Infrastructure;

/// <summary>
/// Measures what this instance's data occupies, from catalog statistics scoped to the configured
/// schema, never by counting rows. Reported in usage telemetry as sizes and counts only.
/// </summary>
public interface IStorageFootprintProbe
{
    /// <summary>
    /// Null when the database could not be asked. A field is null when the catalog had no answer,
    /// such as a row estimate on a table that was never analysed.
    /// </summary>
    Task<StorageFootprint?> Probe(CancellationToken cancellationToken = default);
}

public record StorageFootprint(double? SizeGB, long? MessageCount);
