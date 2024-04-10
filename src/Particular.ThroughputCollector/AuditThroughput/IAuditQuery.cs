namespace Particular.ThroughputCollector.AuditThroughput
{
    using NuGet.Versioning;
    using Particular.ThroughputCollector.Contracts;

    public interface IAuditQuery
    {
        SemanticVersion MinAuditCountsVersion { get; }
        Func<RemoteInstanceInformation, bool> ValidRemoteInstances { get; }

        IEnumerable<ServiceControlEndpoint> GetKnownEndpoints();

        Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName, CancellationToken cancellationToken);
        Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken);
        Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken);

    }
}
