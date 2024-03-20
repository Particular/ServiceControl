namespace Particular.ThroughputCollector.Contracts
{
    using System.Net;

    class ServiceControlEndpoint
    {
        public string Name { get; set; } = string.Empty;
        public string UrlName => WebUtility.UrlEncode(Name);
        public bool HeartbeatsEnabled { get; set; }
    }
}
