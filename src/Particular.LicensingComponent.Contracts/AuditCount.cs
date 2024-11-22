namespace Particular.LicensingComponent.Contracts;

using System.Text.Json.Serialization;

public class AuditCount
{
    [JsonPropertyName("utc_date")]
    public DateOnly UtcDate { get; set; }
    [JsonPropertyName("count")]
    public long Count { get; set; }
}