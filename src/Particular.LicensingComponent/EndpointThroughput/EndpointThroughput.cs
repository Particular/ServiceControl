namespace Particular.LicensingComponent
{
    using Particular.LicensingComponent.Contracts;

    public record EndpointThroughput(string EndpointName, IGrouping<string, Endpoint> EndpointGroupPerQueue, List<ThroughputData> ThroughputData);
}
