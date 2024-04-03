namespace Particular.ThroughputCollector.Contracts;

public class ConnectionSettingsTestResult
{
    public bool ConnectionSuccessful { get; set; }
    public List<string> ConnectionErrorMessages { get; set; } = [];
}

