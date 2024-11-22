namespace Particular.LicensingComponent.Contracts;

using System.Net;

public class ServiceControlEndpoint
{
    public string Name { get; set; } = string.Empty;
    public string UrlName => WebUtility.UrlEncode(Name);
    public bool HeartbeatsEnabled { get; set; }
}