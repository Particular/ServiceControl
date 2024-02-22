namespace Particular.ThroughputCollector.Shared
{
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.Infrastructure;

    class Commands
    {
        static readonly Version MinAuditCountsVersion = new Version(4, 29);

        public static async Task<ServiceControlEndpoint[]> GetKnownEndpoints(ServiceControlClient primary, ILogger logger, CancellationToken cancellationToken)
        {
            // Tool can't proceed without this data, try 5 times
            var arr = await primary.GetData<JArray>("/endpoints", 5, cancellationToken).ConfigureAwait(false);

            var endpoints = arr.Select(endpointToken => new
            {
                Name = endpointToken["name"]?.Value<string>(),
                HeartbeatsEnabled = endpointToken["monitored"]?.Value<bool>(),
                ReceivingHeartbeats = endpointToken["heartbeat_information"]?["reported_status"]!.Value<string>() == "beating"
            })
            .GroupBy(x => x.Name)
            .Select(g => new ServiceControlEndpoint
            {
                Name = g.Key!,
                HeartbeatsEnabled = g.Any(e => e.HeartbeatsEnabled == true),
                ReceivingHeartbeats = g.Any(e => e.ReceivingHeartbeats)
            })
            .ToArray();

            var useAuditCounts = false;

            // Verify audit instances also have audit counts
            var remotesInfoJson = await primary.GetData<JArray>("/configuration/remotes", cancellationToken).ConfigureAwait(false);
            var remoteInfo = remotesInfoJson.Select(remote =>
            {
                var uri = remote["api_uri"]?.Value<string>();
                var status = remote["status"]?.Value<string>();
                var versionString = remote["version"]?.Value<string>();
                var retentionString = remote["configuration"]?["data_retention"]?["audit_retention_period"]?.Value<string>();

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
                    var configUrl = primary.GetFullUrl("/configuration/remotes");
                    var remoteConfigMsg = $"Unable to determine the version of one or more ServiceControl Audit instances. For the instance with URI {remote.Uri}, the status was '{remote.Status}' and the version string returned was '{remote.VersionString}'. If you are not able to resolve this issue on your own, send the contents of {configUrl} to Particular when requesting help.";
                    throw new HaltException(HaltReason.InvalidEnvironment, remoteConfigMsg);
                }
            }

            // Want 2d audit retention so we get one complete UTC day no matter what time it is
            useAuditCounts = remoteInfo.All(r => r.SemVer?.Version >= MinAuditCountsVersion && r.Retention >= TimeSpan.FromDays(2));

            foreach (var endpoint in endpoints)
            {
                if (useAuditCounts)
                {
                    var path = $"/endpoints/{endpoint.UrlName}/audit-count";
                    endpoint.AuditCounts = await primary.GetData<AuditCount[]>(path, 2, cancellationToken).ConfigureAwait(false);
                    endpoint.CheckHourlyAuditDataIfNoMonitoringData = false;
                }
                else
                {
                    var path = $"/endpoints/{endpoint.UrlName}/messages/?per_page=1";
                    var recentMessages = await primary.GetData<JArray>(path, 2, cancellationToken).ConfigureAwait(false);
                    endpoint.CheckHourlyAuditDataIfNoMonitoringData = recentMessages.Any();
                }
            }

            return endpoints;
        }
    }
}
