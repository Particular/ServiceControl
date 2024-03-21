﻿namespace Particular.ThroughputCollector.AuditThroughput
{
    using System.Text.Json.Nodes;
    using Contracts;
    using ServiceControl.Api;
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
            var remotesInfo = new List<RemoteInstanceInformation>();
            var valueType = remotes.GetType();

            if (remotes != null && valueType.IsArray)
            {
                var remoteObjects = (object[])remotes;
                if (remoteObjects.Length > 0)
                {
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
                        }

                        var remoteInstance = new RemoteInstanceInformation
                        {
                            ApiUri = apiUriProp != null ? apiUriProp.GetValue(remote)?.ToString() : "",
                            VersionString = versionProp != null ? versionProp.GetValue(remote)?.ToString() : "",
                            Status = statusProp != null ? statusProp.GetValue(remote)?.ToString() : "",
                            Retention = TimeSpan.TryParse(retention, out var ts) ? ts : TimeSpan.Zero
                        };

                        remoteInstance.SemVer = SemVerVersion.TryParse(remoteInstance.VersionString, out var v) ? v : null;

                        remotesInfo.Add(remoteInstance);
                    }
                }
            }

            return remotesInfo;
        }

        public static async Task<ConnectionSettingsTestResult> TestAuditConnection(IConfigurationApi configurationApi)
        {
            var connectionTestResult = new ConnectionSettingsTestResult();

            var remotesInfo = await GetAuditRemotes(configurationApi);

            foreach (var remote in remotesInfo.Where(w => w.Status != "online" && w.SemVer == null))
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
