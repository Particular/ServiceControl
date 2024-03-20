namespace Particular.ThroughputCollector.Contracts;

public class RemoteInstanceInformation
{
    public string? ApiUri { get; set; }
    public string? VersionString { get; set; }
    public string? Status { get; set; }
    public SemVerVersion? SemVer { get; set; }
    public TimeSpan Retention { get; set; }
}
