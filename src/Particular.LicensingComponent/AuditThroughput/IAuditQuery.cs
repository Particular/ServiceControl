namespace Particular.LicensingComponent.AuditThroughput
{
    using NuGet.Versioning;
    using Particular.LicensingComponent.Contracts;

    public interface IAuditQuery
    {
        SemanticVersion MinAuditCountsVersion { get; }
        Func<RemoteInstanceInformation, bool> ValidRemoteInstances { get; }

        Task<IEnumerable<ServiceControlEndpoint>> GetKnownEndpoints(CancellationToken cancellationToken = default);

        Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName, CancellationToken cancellationToken = default);
        Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken = default);
        Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken = default);

    }
}
