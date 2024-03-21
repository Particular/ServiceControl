namespace Particular.ThroughputCollector.AuditThroughput
{
    using Particular.ThroughputCollector.Contracts;
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;
    using AuditCount = Contracts.AuditCount;

    static class AuditCommands
    {
        // Customers are expected to run at least version 4.29 for their Audit instances
        public static readonly Version MinAuditCountsVersion = new Version(4, 29);
        // Want 2d audit retention so we get one complete UTC day no matter what time it is.
        public static Func<RemoteInstanceInformation, bool> ValidRemoteInstances = r => r.SemVer?.Version >= MinAuditCountsVersion && r.Retention >= TimeSpan.FromDays(2);

        public static async Task<ServiceControlEndpoint[]> GetKnownEndpoints(IEndpointsApi endpointsApi)
        {
            var endpoints = await endpointsApi.GetEndpoints();

            var scEndpoints = endpoints?.Select(endpoint => new
            {
                Name = endpoint.Name ?? "",
                HeartbeatsEnabled = endpoint.Monitored
            })
            .GroupBy(x => x.Name)
            .Select(g => new ServiceControlEndpoint
            {
                Name = g.Key!,
                HeartbeatsEnabled = g.Any(e => e.HeartbeatsEnabled),
            })
            .ToArray();

            return scEndpoints ?? Array.Empty<ServiceControlEndpoint>();
        }

        public static async Task<List<AuditCount>> GetAuditCountForEndpoint(IAuditCountApi auditCountApi, string endpointUrlName)
        {
            return (await auditCountApi.GetEndpointAuditCounts(endpointUrlName)).Select(s =>
            {
                return new AuditCount { Count = s.Count, UtcDate = DateOnly.FromDateTime(s.UtcDate) };
            }).ToList();
        }

        public static async Task<List<RemoteInstanceInformation>> GetAuditRemotes(IConfigurationApi configurationApi)
        {
            // Verify audit instances also have audit counts
            var remotes = await configurationApi.GetRemoteConfigs();

            return remotes.Select(configuration => new RemoteInstanceInformation
            {
                ApiUri = configuration.ApiUri,
                VersionString = configuration.Version,
                Status = configuration.Status,
                Retention = configuration.Configuration.DataRetention.AuditRetentionPeriod,
                SemVer = SemVerVersion.TryParse(configuration.Version, out var v) ? v : null
            }).ToList();
        }

        public static async Task<ConnectionSettingsTestResult> TestAuditConnection(IConfigurationApi configurationApi)
        {
            var connectionTestResult = new ConnectionSettingsTestResult();

            var remotesInfo = await GetAuditRemotes(configurationApi);

            foreach (var remote in remotesInfo.Where(w => w.Status != RemoteStatus.Online && w.SemVer == null))
            {
                connectionTestResult.ConnectionErrorMessages.Add($"Unable to determine the version of one or more ServiceControl Audit instances. For the instance with URI {remote.ApiUri}, the status was '{remote.Status}' and the version string returned was '{remote.VersionString}'.");
            }

            var allHaveAuditCounts = remotesInfo.All(ValidRemoteInstances);
            if (!allHaveAuditCounts)
            {
                connectionTestResult.ConnectionErrorMessages.Add($"At least one ServiceControl Audit instance is either not running the required version ({MinAuditCountsVersion}) or is not configured for at least 2 days of retention. Audit throughput will not be available.");
            }

            connectionTestResult.ConnectionSuccessful = !connectionTestResult.ConnectionErrorMessages.Any();

            return connectionTestResult;
        }
    }
}
