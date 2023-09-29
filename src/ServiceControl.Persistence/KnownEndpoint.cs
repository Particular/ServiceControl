namespace ServiceControl.Persistence
{
    using ServiceControl.Operations;

    public class KnownEndpoint
    {
        public string HostDisplayName { get; set; }
        public bool Monitored { get; set; }
        public EndpointDetails EndpointDetails { get; set; }
        public bool HasTemporaryId { get; set; }

        public const string CollectionName = "KnownEndpoints";
    }
}