namespace ServiceControl.Monitoring
{
    using System;
    using Contracts.Operations;

    public class KnownEndpoint
    {
        public Guid Id { get; set; }
        public string HostDisplayName { get; set; }
        public bool Monitored { get; set; }
        public EndpointDetails EndpointDetails { get; set; }
        public bool HasTemporaryId { get; set; }

        public const string CollectionName = "KnownEndpoints";
    }
}