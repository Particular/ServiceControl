namespace ServiceControl.Persistence
{
    using ServiceControl.Operations;

    public class KnownEndpoint
    {
        public string? HostDisplayName { get; set; }
        public bool Monitored { get; set; }
        public required EndpointDetails EndpointDetails { get; set; }
    }
}