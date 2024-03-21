namespace Particular.ThroughputCollector.Contracts;

using System.Text.Json.Serialization;

public class ReportGenerationState
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Broker Broker { get; set; }
    public bool ReportCanBeGenerated { get; set; }
}
