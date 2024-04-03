namespace Particular.ThroughputCollector.AuditThroughput
{
    using System.Text.Json.Nodes;
    using Contracts;
    using NuGet.Versioning;
    using ServiceControl.Api;
    using AuditCount = Contracts.AuditCount;

    public class AuditQuery(IEndpointsApi endpointsApi, IAuditCountApi auditCountApi, IConfigurationApi configurationApi)
    {
        // Customers are expected to run at least version 4.29 for their Audit instances
        public static readonly SemanticVersion MinAuditCountsVersion = new(4, 29, 0);
        // Want 2d audit retention so we get one complete UTC day no matter what time it is.
        public static Func<RemoteInstanceInformation, bool> ValidRemoteInstances = r =>
            r.SemanticVersion != null &&
            r.SemanticVersion >= MinAuditCountsVersion &&
            r.Retention >= TimeSpan.FromDays(2);

        public IEnumerable<ServiceControlEndpoint> GetKnownEndpoints()
        {
            var endpoints = endpointsApi.GetEndpoints();

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
            });

            return scEndpoints ?? [];
        }

        public async Task<IEnumerable<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName, CancellationToken cancellationToken) => (await auditCountApi.GetEndpointAuditCounts(endpointUrlName, cancellationToken)).Select(s => new AuditCount { Count = s.Count, UtcDate = DateOnly.FromDateTime(s.UtcDate) });

        public async Task<List<RemoteInstanceInformation>> GetAuditRemotes(CancellationToken cancellationToken)
        {
            var remotes = await configurationApi.GetRemoteConfigs(cancellationToken);
            var remotesInfo = new List<RemoteInstanceInformation>();
            var valueType = remotes.GetType();

            if (remotes != null && valueType.IsArray)
            {
                var remoteObjects = (object[])remotes;
                if (remoteObjects.Length > 0)
                {
                    List<string> queues = [];
                    var props = remoteObjects[0].GetType().GetProperties();

                    var apiUriProp = props.FirstOrDefault(w => w.Name == "ApiUri");
                    var versionProp = props.FirstOrDefault(w => w.Name == "Version");
                    var statusProp = props.FirstOrDefault(w => w.Name == "Status");
                    var configurationProp = props.FirstOrDefault(w => w.Name == "Configuration");

                    foreach (var remote in remoteObjects)
                    {
                        var config = configurationProp != null ? configurationProp.GetValue(remote) as JsonNode : null;
                        string? retention = null;
                        if (config != null)
                        {
                            retention = config?.AsObject().TryGetPropertyValue("data_retention", out var dataRetention) == true &&
                                        dataRetention?.AsObject().TryGetPropertyValue("audit_retention_period", out var auditRetentionPeriod) == true
                                        ? auditRetentionPeriod!.GetValue<string>()
                                        : null;

                            if (config?.AsObject().TryGetPropertyValue("host", out var host) == true &&
                                        host?.AsObject().TryGetPropertyValue("service_name", out var serviceName) == true)
                            {
                                queues.Add(serviceName!.GetValue<string>());
                            }

                            if (config?.AsObject().TryGetPropertyValue("transport", out var transport) == true)
                            {
                                if (transport?.AsObject().TryGetPropertyValue("audit_queue", out var auditQueue) == true)
                                {
                                    queues.Add(auditQueue!.GetValue<string>());
                                }
                                if (transport?.AsObject().TryGetPropertyValue("audit_log_queue", out var auditLogQueue) == true)
                                {
                                    queues.Add(auditLogQueue!.GetValue<string>());
                                }
                            }
                        }

                        var remoteInstance = new RemoteInstanceInformation
                        {
                            ApiUri = apiUriProp != null ? apiUriProp.GetValue(remote)?.ToString() : "",
                            VersionString = versionProp != null ? versionProp.GetValue(remote)?.ToString() : "",
                            Status = statusProp != null ? statusProp.GetValue(remote)?.ToString() : "",
                            Retention = TimeSpan.TryParse(retention, out var ts) ? ts : TimeSpan.Zero,
                            Queues = queues
                        };

                        remoteInstance.SemanticVersion = SemanticVersion.TryParse(remoteInstance.VersionString ?? string.Empty, out var v) ? v : null;

                        remotesInfo.Add(remoteInstance);
                    }
                }
            }

            return remotesInfo;
        }

        public async Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken)
        {
            var connectionTestResult = new ConnectionSettingsTestResult();

            var remotesInfo = await GetAuditRemotes(cancellationToken);

            if (!remotesInfo.Any())
            {
                connectionTestResult.ConnectionErrorMessages.Add("No Audit Instances configured");
            }

            foreach (var remote in remotesInfo.Where(w => w.Status != "online" && w.SemanticVersion == null))
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
