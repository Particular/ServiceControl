namespace ServiceControl.Monitoring
{
    using Contracts.Operations;

    public class KnownEndpoint
    {
        public string Id { get; set; }
        public string HostDisplayName { get; set; }
        public bool Monitored { get; set; }
        public EndpointDetails EndpointDetails { get; set; }
        public bool HasTemporaryId { get; set; }
    }
}