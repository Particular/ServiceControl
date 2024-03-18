namespace Particular.ThroughputCollector.Shared
{
    using System.Linq;
    using System.Text.Json.Nodes;
    using Contracts;
    using Infrastructure;
    using Microsoft.Extensions.Logging;
    using ServiceControl.Api;

    class ServiceControlCommands
    {
        static readonly Version MinAuditCountsVersion = new Version(4, 29);

        public static async Task<ServiceControlEndpoint[]> GetKnownEndpoints(IConfigurationApi configurationApi, IEndpointsApi endpointsApi, IAuditCountApi auditCountApi, ILogger logger)
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
                    var VersionProp = props.FirstOrDefault(w => w.Name == "Version");
                    var statusProp = props.FirstOrDefault(w => w.Name == "Status");
                    var configurationProp = props.FirstOrDefault(w => w.Name == "Configuration");

                    foreach (var remote in remoteObjects)
                    {
                        var config = configurationProp != null ? configurationProp.GetValue(remote) as JsonNode : null;
                        string? retention = null;
                        if (config != null)
                        {
                            retention = config?.AsObject().TryGetPropertyValue("data_retention", out _) == true &&
                                        config?.AsObject().TryGetPropertyValue("audit_retention_period", out var auditRetentionPeriod) == true
                                        ? auditRetentionPeriod!.GetValue<string>()
                                        : null;
                        }

                        var remoteInstance = new RemoteInstanceInformation
                        {
                            ApiUri = apiUriProp != null ? apiUriProp.GetValue(remote)?.ToString() : "",
                            VersionString = VersionProp != null ? VersionProp.GetValue(remote)?.ToString() : "",
                            Status = statusProp != null ? statusProp.GetValue(remote)?.ToString() : "",
                            Retention = TimeSpan.TryParse(retention, out var ts) ? ts : TimeSpan.Zero
                        };

                        remoteInstance.SemVer = SemVerVersion.TryParse(remoteInstance.VersionString, out var v) ? v : null;

                        remotesInfo.Add(remoteInstance);
                    }
                }
            }

            foreach (var remote in remotesInfo)
            {
                if (remote.Status == "online" || remote.SemVer is not null)
                {
                    logger.LogInformation($"ServiceControl Audit instance at {remote.ApiUri} detected running version {remote.SemVer}");
                }
                else
                {
                    var remoteConfigMsg = $"Unable to determine the version of one or more ServiceControl Audit instances. For the instance with URI {remote.ApiUri}, the status was '{remote.Status}' and the version string returned was '{remote.VersionString}'.";
                }
            }

            // Want 2d audit retention so we get one complete UTC day no matter what time it is.
            // Customers are expected to run at least version 4.29 for their Audit instances
            var allHaveAuditCounts = remotesInfo.All(r => r.SemVer?.Version >= MinAuditCountsVersion && r.Retention >= TimeSpan.FromDays(2));
            if (!allHaveAuditCounts)
            {
                logger.LogWarning($"At least one ServiceControl Audit instance is either not running the required version ({MinAuditCountsVersion}) or is not configured for at least 2 days of retention. Audit throughput will not be available.");
            }

            if (scEndpoints != null)
            {
                foreach (var endpoint in scEndpoints)
                {
                    try
                    {
                        endpoint.AuditCounts = (await auditCountApi.GetEndpointAuditCounts(page: null, pageSize: null, endpoint: endpoint.UrlName)).Select(s =>
                        {
                            return new AuditCount { Count = s.Count, UtcDate = s.UtcDate };
                        }).ToArray();
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, $"Audit count not available on endpoint {endpoint.Name}");
                    }
                }
            }

            return scEndpoints ?? Array.Empty<ServiceControlEndpoint>();
        }

        public static async Task<ServiceControlEndpoint[]> GetKnownEndpoints(ServiceControlClient primary, ILogger logger, CancellationToken cancellationToken)
        {
            // Tool can't proceed without this data, try 5 times
            var arr = await primary.GetData<JsonArray>("/endpoints", 5, cancellationToken);

            var endpoints = arr?.Select(endpoint => new
            {
                Name =
                    endpoint?.AsObject().TryGetPropertyValue("name", out var name) == true
                        ? name?.GetValue<string>()
                        : "",
                HeartbeatsEnabled =
                    endpoint?.AsObject().TryGetPropertyValue("monitored", out var monitored) == true
                        ? monitored?.GetValue<bool>()
                        : false,
                ReceivingHeartbeats =
                    endpoint?.AsObject().TryGetPropertyValue("heartbeat_information", out var heartbeats) == true &&
                    heartbeats?.AsObject().TryGetPropertyValue("reported_status", out var reportStatus) == true &&
                    reportStatus!.GetValue<string>() == "beating"
            })
            .GroupBy(x => x.Name)
            .Select(g => new ServiceControlEndpoint
            {
                Name = g.Key!,
                HeartbeatsEnabled = g.Any(e => e.HeartbeatsEnabled == true),
                //ReceivingHeartbeats = g.Any(e => e.ReceivingHeartbeats)
            })
            .ToArray();

            // Verify audit instances also have audit counts
            var remotesInfoJson = await primary.GetData<JsonArray>("/configuration/remotes", cancellationToken);
            var remoteInfo = remotesInfoJson.Select(remote =>
            {
                var uri = remote?.AsObject().TryGetPropertyValue("api_uri", out var apiUrl) == true
                    ? apiUrl?.GetValue<string>()
                    : null;
                var status = remote?.AsObject().TryGetPropertyValue("status", out var statusVal) == true
                    ? statusVal?.GetValue<string>()
                    : null;
                var versionString = remote?.AsObject().TryGetPropertyValue("version", out var version) == true
                    ? version?.GetValue<string>()
                    : null;
                var retentionString =
                    remote?.AsObject().TryGetPropertyValue("configuration", out var configuration) == true &&
                    configuration?.AsObject().TryGetPropertyValue("data_retention", out _) == true &&
                    configuration?.AsObject()
                        .TryGetPropertyValue("audit_retention_period", out var auditRetentionPeriod) == true
                        ? auditRetentionPeriod!.GetValue<string>()
                        : null;
                return new
                {
                    Uri = uri,
                    Status = status,
                    VersionString = versionString,
                    SemVer = SemVerVersion.TryParse(versionString, out var v) ? v : null,
                    Retention = TimeSpan.TryParse(retentionString, out var ts) ? ts : TimeSpan.Zero
                };
            })
            .ToArray();

            foreach (var remote in remoteInfo)
            {
                if (remote.Status == "online" || remote.SemVer is not null)
                {
                    logger.LogInformation($"ServiceControl Audit instance at {remote.Uri} detected running version {remote.SemVer}");
                }
                else
                {
                    logger.LogWarning($"Unable to determine the version of one or more ServiceControl Audit instances. For the instance with URI {remote.Uri}, the status was '{remote.Status}' and the version string returned was '{remote.VersionString}'.");
                }
            }

            // Want 2d audit retention so we get one complete UTC day no matter what time it is.
            // Customers are expected to run at least version 4.29 for their Audit instances
            var allHaveAuditCounts = remoteInfo.All(r => r.SemVer?.Version >= MinAuditCountsVersion && r.Retention >= TimeSpan.FromDays(2));
            if (!allHaveAuditCounts)
            {
                logger.LogWarning($"At least one ServiceControl Audit instance is either not running the required version ({MinAuditCountsVersion}) or is not configured for at least 2 days of retention. Audit throughput will not be available.");
            }

            if (endpoints != null)
            {
                foreach (var endpoint in endpoints)
                {
                    var path = $"/endpoints/{endpoint.UrlName}/audit-count";
                    try
                    {
                        endpoint.AuditCounts = await primary.GetData<AuditCount[]>(path, 2, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, $"Audit count not available on endpoint {endpoint.Name} at {path}");
                    }
                }
            }

            return endpoints ?? Array.Empty<ServiceControlEndpoint>();
        }
    }
}
