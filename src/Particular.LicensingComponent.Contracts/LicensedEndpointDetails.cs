namespace Particular.LicensingComponent.Contracts
{
    public class LicensedEndpointDetails
    {
        public string? LicenseId { get; set; }
        public LicensedEndpoint[] Endpoints { get; set; } = [];
        public QueueIdentity[] InfrastructureQueues { get; set; } = [];
        public QueueIdentity[] ExcludedQueues { get; set; } = [];
        public QueueIdentity[] UnassignedQueues { get; set; } = [];
        public string? ServiceEndDate { get; set; }
        public Product[] Products { get; set; } = [];
        public bool ValidId { get; set; }
    }

    public class LicensedEndpointDetailsAmendment
    {
        public LicensedEndpointAmendment[] Endpoints { get; set; } = [];
        public string[] RemovedEndpointIds { get; set; } = [];
        public QueueIdentity[] InfrastructureQueues { get; set; } = [];
        public QueueIdentity[] ExcludedQueues { get; set; } = [];
        public QueueIdentity[] UnassignedQueues { get; set; } = [];
        public QueueIdentity[] UnmappedQueues { get; set; } = [];

    }

    public class Product
    {
        public string? ProductCode { get; set; }
        public int? MonthlyThroughput { get; set; }
    }

    public record QueueIdentity
    {
        public required string NameHash { get; set; }
        public string? ScopeHash { get; set; }
        public string? OriginalLocation { get; set; }
    }

    public record LicensedEndpoint
    {
        public required string ClientId { get; set; }
        public required string Name { get; set; }
        public required int Classification { get; set; }
        public string? EndpointSize { get; set; }

        public QueueIdentity[] Queues { get; set; } = [];
    }

    public class LicensedEndpointAmendment
    {
        public required string ClientId { get; set; }
        public required string Name { get; set; }
        public required int Classification { get; set; }
        public QueueIdentity[] AmendedQueues { get; set; } = [];
    }
}
