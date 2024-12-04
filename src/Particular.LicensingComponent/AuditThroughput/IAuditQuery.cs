namespace Particular.LicensingComponent.AuditThroughput
{
    using NuGet.Versioning;
    using Particular.LicensingComponent.Contracts;

    public interface IAuditQuery
    {
        SemanticVersion MinAuditCountsVersion { get; }
        Func<RemoteInstanceInformation, bool> ValidRemoteInstances { get; }

        Task<IEnumerable<ServiceControlEndpoint>> GetKnownEndpoints(CancellationToken cancellationToken);

        Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName, CancellationToken cancellationToken);
        Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken);
        Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken);

    }
}