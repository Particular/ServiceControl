namespace Particular.ThroughputCollector.Contracts;

using System.Text.Json.Serialization;

public class ConnectionSettingsTestResult
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public bool ConnectionSuccessful { get; set; }
    public List<string> ConnectionErrorMessages { get; set; } = [];
}

