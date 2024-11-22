namespace Particular.LicensingComponent.AuditThroughput
{
    using System.Text;
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

        public async Task<IEnumerable<ServiceControlEndpoint>> GetKnownEndpoints(CancellationToken cancellationToken)
        {
            var endpoints = await endpointsApi.GetEndpoints(cancellationToken);

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

                if (remotes.Any())
                {
                    List<string> queues = [];

                    foreach (var remote in remotes)
                    {
                        string? retention = null;
                        string? transportTypeUsed = null;
                        if (remote.Configuration != null)
                        {
                            retention = remote.Configuration.AsObject().TryGetPropertyValue("data_retention", out var dataRetention) &&
                                        dataRetention?.AsObject().TryGetPropertyValue("audit_retention_period", out var auditRetentionPeriod) == true
                                        ? auditRetentionPeriod!.GetValue<string>()
                                        : null;

                            if (remote.Configuration.AsObject().TryGetPropertyValue("host", out var host) &&
                                        host?.AsObject().TryGetPropertyValue("service_name", out var serviceName) == true)
                            {
                                queues.Add(serviceName!.GetValue<string>());
                            }

                            if (remote.Configuration.AsObject().TryGetPropertyValue("transport", out var transport))
                            {
                                if (transport?.AsObject().TryGetPropertyValue("audit_queue", out var auditQueue) == true)
                                {
                                    queues.Add(auditQueue!.GetValue<string>());
                                }
                                if (transport?.AsObject().TryGetPropertyValue("audit_log_queue", out var auditLogQueue) == true)
                                {
                                    queues.Add(auditLogQueue!.GetValue<string>());
                                }
                                if (transport?.AsObject().TryGetPropertyValue("transport_type", out var transportType) == true)
                                {
                                    transportTypeUsed = transportType!.GetValue<string>();
                                }
                            }
                        }

                        var remoteInstance = new RemoteInstanceInformation
                        {
                            ApiUri = remote.ApiUri,
                            VersionString = remote.Version,
                            Status = remote.Status,
                            Retention = TimeSpan.TryParse(retention, out var ts) ? ts : TimeSpan.Zero,
                            Queues = queues,
                            Transport = transportTypeUsed ?? ""
                        };

                        remoteInstance.SemanticVersion = SemanticVersion.TryParse(remoteInstance.VersionString ?? string.Empty, out var v) ? v : null;

                        remotesInfo.Add(remoteInstance);
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

            foreach (var remote in remotesInfo)
            {
                if (remote.Status != "online" && remote.SemanticVersion == null)
                {
                    connectionTestResult.ConnectionErrorMessages.Add($"{remote.ApiUri} - version of ServiceControl Audit instance could not be determined, hence audit throughput data will not be available.");
                }
                else
                {
                    if (remote.SemanticVersion == null || remote.SemanticVersion < MinAuditCountsVersion)
                    {
                        connectionTestResult.ConnectionErrorMessages.Add($"{remote.ApiUri} - version ({remote.VersionString}) of ServiceControl Audit instance is too old (minimum version is {MinAuditCountsVersion}), hence audit throughput data will not be available.");
                    }
                    if (remote.Retention < TimeSpan.FromDays(2))
                    {
                        connectionTestResult.ConnectionErrorMessages.Add($"{remote.ApiUri} - retention period of ServiceControl Audit instance is set to {remote.Retention.Days} days (minimum is 2 days),  hence audit throughput data will not be available.");
                    }
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