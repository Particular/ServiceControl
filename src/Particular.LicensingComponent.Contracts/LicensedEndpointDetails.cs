namespace Particular.LicensingComponent.Contracts
{
    public class LicensedEndpointDetails
    {
        public LicensedEndpoint[] Endpoints { get; set; } = [];
        public QueueIdentity[] InfrastructureQueues { get; set; } = [];
        public QueueIdentity[] ExcludedQueues { get; set; } = [];
        public string? ServiceEndDate { get; set; }
        public Product[] Products { get; set; } = [];
    }

    public class Product
    {
        public string? ProductCode { get; set; }
        public int? MonthlyThroughput { get; set; }
    }

    public class QueueIdentity
    {
        public string? NameHash { get; set; }
        public string? Scope { get; set; }
    }

    public class LicensedEndpoint
    {
        public string? Name { get; set; }
        public int? Classification { get; set; }
        public string? EndpointSize { get; set; }
        public QueueIdentity[] Queues { get; set; } = [];
    }
}
