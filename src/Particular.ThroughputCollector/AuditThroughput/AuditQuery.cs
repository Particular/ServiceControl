namespace Particular.ThroughputCollector.AuditThroughput
{
    using System.Text;
    using System.Text.Json.Nodes;
    using Contracts;
    using Microsoft.Extensions.Logging;
    using NuGet.Versioning;
    using ServiceControl.Api;
    using AuditCount = Contracts.AuditCount;

    public class AuditQuery(ILogger<AuditQuery> logger, IEndpointsApi endpointsApi, IAuditCountApi auditCountApi, IConfigurationApi configurationApi) : IAuditQuery
    {
        // Customers are expected to run at least version 4.29 for their Audit instances
        public SemanticVersion MinAuditCountsVersion => new(4, 29, 0);
        // Want 2d audit retention so we get one complete UTC day no matter what time it is.        
        public Func<RemoteInstanceInformation, bool> ValidRemoteInstances => r =>
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
            try
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
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get Audit Remotes");
                return [];
            }
        }

        public async Task<ConnectionSettingsTestResult> TestAuditConnection(CancellationToken cancellationToken)
        {
            var connectionTestResult = new ConnectionSettingsTestResult { ConnectionSuccessful = true };

            var remotesInfo = await GetAuditRemotes(cancellationToken);

            if (!remotesInfo.Any())
            {
                connectionTestResult.Diagnostics = "No Audit Instances configured";
                return connectionTestResult;
            }

            foreach (var remote in remotesInfo.Where(w => w.Status != "online" && w.SemanticVersion == null))
            {
                connectionTestResult.ConnectionErrorMessages.Add($"{remote.ApiUri} - unable to determine the version of ServiceControl Audit instance. The instance status is '{remote.Status}' and the version is '{remote.VersionString}'.");
            }

            foreach (var remote in remotesInfo.Where(w => !ValidRemoteInstances(w)))
            {
                if (remote.SemanticVersion == null || remote.SemanticVersion < MinAuditCountsVersion)
                {
                    connectionTestResult.ConnectionErrorMessages.Add($"{remote.ApiUri} - ServiceControl Audit instance with invalid version of '{remote.VersionString}' (minimum version is {MinAuditCountsVersion}). Audit throughput will not be available.");
                }
                if (remote.Retention < TimeSpan.FromDays(2))
                {
                    connectionTestResult.ConnectionErrorMessages.Add($"{remote.ApiUri} - ServiceControl Audit instance with invalid retention period configuration of {remote.Retention.Days} days (minimum is 2 days). Audit throughput will not be available.");
                }
            }

            connectionTestResult.ConnectionSuccessful = !connectionTestResult.ConnectionErrorMessages.Any();

            var diagnostics = new StringBuilder();
            if (connectionTestResult.ConnectionSuccessful)
            {
                diagnostics.AppendLine($"Connection test to Audit Instance{(remotesInfo.Count == 1 ? "" : "(s)")} was successful");
                diagnostics.AppendLine();
                diagnostics.AppendLine("Connected to the following Audit instances:");
            }
            else
            {
                diagnostics.AppendLine($"Connection test to Audit Instance{(remotesInfo.Count == 1 ? "" : "(s)")} failed:");
                connectionTestResult.ConnectionErrorMessages.ForEach(e => diagnostics.AppendLine(e));
                diagnostics.AppendLine();
                diagnostics.AppendLine("Connection attempted to the following Audit instances:");
            }

            remotesInfo.ForEach(r => diagnostics.AppendLine(r.ApiUri));


            connectionTestResult.Diagnostics = diagnostics.ToString();

            return connectionTestResult;
        }
    }
}
