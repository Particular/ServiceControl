namespace ServiceControl.AcceptanceTesting.ForwardedHeaders
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Response DTO for the /debug/request-info endpoint.
    /// Used by forwarded headers acceptance tests to verify request processing.
    /// Shared across all instance types (Primary, Audit, Monitoring).
    /// </summary>
    public class RequestInfoResponse
    {
        [JsonPropertyName("processed")]
        public ProcessedInfo Processed { get; set; }

        [JsonPropertyName("rawHeaders")]
        public RawHeadersInfo RawHeaders { get; set; }

        [JsonPropertyName("configuration")]
        public ConfigurationInfo Configuration { get; set; }
    }

    public class ProcessedInfo
    {
        [JsonPropertyName("scheme")]
        public string Scheme { get; set; }

        [JsonPropertyName("host")]
        public string Host { get; set; }

        [JsonPropertyName("remoteIpAddress")]
        public string RemoteIpAddress { get; set; }
    }

    public class RawHeadersInfo
    {
        [JsonPropertyName("xForwardedFor")]
        public string XForwardedFor { get; set; }

        [JsonPropertyName("xForwardedProto")]
        public string XForwardedProto { get; set; }

        [JsonPropertyName("xForwardedHost")]
        public string XForwardedHost { get; set; }
    }

    public class ConfigurationInfo
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("trustAllProxies")]
        public bool TrustAllProxies { get; set; }

        [JsonPropertyName("knownProxies")]
        public string[] KnownProxies { get; set; }

        [JsonPropertyName("knownNetworks")]
        public string[] KnownNetworks { get; set; }
    }
}
