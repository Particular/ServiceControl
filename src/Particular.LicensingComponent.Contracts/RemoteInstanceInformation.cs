namespace Particular.LicensingComponent.Contracts;

using NuGet.Versioning;

public class RemoteInstanceInformation
{
    public string? ApiUri { get; set; }
    public string? VersionString { get; set; }
    public string? Status { get; set; }
    public List<string> Queues { get; set; } = [];
    public SemanticVersion? SemanticVersion { get; set; }
    public TimeSpan Retention { get; set; }
    public string? Transport { get; set; }
}