namespace ServiceControl.PlatformHealth;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Api;
using Api.Contracts;
using Infrastructure;
using Infrastructure.WebApi;
using NServiceBus.Hosting;
using NServiceBus.Logging;
using ServiceBus.Management.Infrastructure.Settings;

sealed class PlatformHealthApi(
    Settings settings,
    HostInformation hostInformation,
    PlatformHealthState state,
    IConfigurationApi configurationApi,
    TimeProvider timeProvider) : IPlatformHealthApi
{
    public async Task<PlatformHealthView> GetHealth(string baseUrl, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var observedAt = timeProvider.GetUtcNow();
        var remotes = await GetRemoteConfigurations(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var instances = new List<PlatformHealthInstance>
        {
            new()
            {
                Id = settings.InstanceId,
                Name = settings.InstanceName,
                Kind = "error",
                Role = "primary-error",
                ApiUrl = baseUrl.TrimEnd('/') + "/",
                Version = ServiceControlVersion.GetFileVersion(),
                HostId = hostInformation.HostId,
                Health = "healthy",
                ObservedAt = observedAt,
                MetadataObservedAt = observedAt,
                HealthSignalsStatus = settings.DisableHealthChecks ? "disabled" : "unreported",
                TransportType = settings.TransportType,
                ErrorQueue = settings.ErrorQueue,
                ErrorLogQueue = settings.ErrorLogQueue,
                ForwardErrorMessages = settings.ForwardErrorMessages,
                ErrorRetentionPeriod = settings.ErrorRetentionPeriod,
                AuditRetentionPeriod = settings.AuditRetentionPeriod
            }
        };

        foreach (var remote in settings.RemoteInstances.DistinctBy(remote => remote.InstanceId).OrderBy(remote => remote.InstanceId, StringComparer.Ordinal))
        {
            var configuration = remotes.FirstOrDefault(candidate => candidate.ApiUri == remote.BaseAddress);
            instances.Add(GetRemoteInstance(remote, configuration, observedAt));
        }

        var reports = state.GetChecks();
        var response = PlatformHealthState.GetHealth(reports);
        var assignments = new Dictionary<Guid, string>();
        var ambiguousInstances = new HashSet<string>(StringComparer.Ordinal);
        foreach (var report in reports)
        {
            var namedInstances = instances.Where(instance => string.Equals(instance.Name, report.InstanceName, StringComparison.OrdinalIgnoreCase)).ToArray();
            var exactMatches = namedInstances.Where(instance => instance.HostId == report.HostId).ToArray();
            if (exactMatches.Length == 1)
            {
                assignments[report.Id] = exactMatches[0].Id;
                continue;
            }

            var reportingHosts = reports.Where(candidate => string.Equals(candidate.InstanceName, report.InstanceName, StringComparison.OrdinalIgnoreCase))
                .Select(candidate => candidate.HostId).Distinct().Take(2).Count();
            if (exactMatches.Length == 0 && namedInstances.Length == 1 && namedInstances[0].HostId is null && reportingHosts == 1)
            {
                assignments[report.Id] = namedInstances[0].Id;
                continue;
            }

            foreach (var instance in namedInstances)
            {
                ambiguousInstances.Add(instance.Id);
            }
        }

        foreach (var alert in response.Alerts)
        {
            alert.InstanceId = assignments.GetValueOrDefault(alert.Id);
        }

        response.Instances = instances.Select(instance =>
        {
            var associated = reports.Where(report => assignments.GetValueOrDefault(report.Id) == instance.Id).ToArray();
            var issues = response.Alerts.Where(alert => alert.InstanceId == instance.Id).ToArray();
            return instance with
            {
                Health = instance.Health == "unavailable" ? "unavailable" : issues.Length == 0 ? "healthy" : "degraded",
                HealthSignalsStatus = instance.HealthSignalsStatus == "disabled" ? "disabled" :
                    ambiguousInstances.Contains(instance.Id) ? "ambiguous" : associated.Length == 0 ? "unreported" : "reported",
                LastReportedAt = associated.Length == 0 ? null : new DateTimeOffset(DateTime.SpecifyKind(associated.Max(report => report.ReportedAt), DateTimeKind.Utc)),
                Issues = issues
            };
        }).ToArray();
        return response;
    }

    PlatformHealthInstance GetRemoteInstance(RemoteInstanceSetting remote, RemoteConfiguration configuration, DateTimeOffset observedAt)
    {
        if (configuration?.Status == "online" && configuration.Configuration != null)
        {
            try
            {
                var metadata = configuration.Configuration.Deserialize<InstanceConfiguration>(SerializerOptions.Default);
                if (!string.IsNullOrWhiteSpace(metadata?.Host?.InstanceName))
                {
                    var kind = metadata.InstanceType switch
                    {
                        "error" => "error",
                        "audit" => "audit",
                        null when metadata.DataRetention?.ErrorRetentionPeriod != null => "error",
                        null when metadata.DataRetention?.AuditRetentionPeriod != null => "audit",
                        _ => "unknown"
                    };
                    var instance = new PlatformHealthInstance
                    {
                        Id = remote.InstanceId,
                        Name = metadata.Host.InstanceName,
                        Kind = kind,
                        Role = "remote-" + kind,
                        ApiUrl = remote.BaseAddress.TrimEnd('/') + "/api/",
                        Version = configuration.Version is null or "Unknown" or "Missing" or "" ? null : configuration.Version,
                        HostId = metadata.Host.HostId is null || metadata.Host.HostId == Guid.Empty ? null : metadata.Host.HostId,
                        Health = "healthy",
                        ObservedAt = observedAt,
                        MetadataObservedAt = observedAt,
                        HealthSignalsStatus = metadata.HealthChecksEnabled == false ? "disabled" : "unreported",
                        TransportType = metadata.Transport?.TransportType,
                        ErrorQueue = metadata.Transport?.ErrorQueue,
                        ErrorLogQueue = metadata.Transport?.ErrorLogQueue,
                        ForwardErrorMessages = metadata.Transport?.ForwardErrorMessages,
                        AuditQueue = metadata.Transport?.AuditQueue,
                        AuditLogQueue = metadata.Transport?.AuditLogQueue,
                        ForwardAuditMessages = metadata.Transport?.ForwardAuditMessages,
                        ErrorRetentionPeriod = metadata.DataRetention?.ErrorRetentionPeriod,
                        AuditRetentionPeriod = metadata.DataRetention?.AuditRetentionPeriod
                    };
                    return lastKnownRemotes.AddOrUpdate(remote.InstanceId, instance,
                        (_, previous) => instance.MetadataObservedAt >= previous.MetadataObservedAt ? instance : previous);
                }
            }
            catch (JsonException exception)
            {
                log.Warn("Unable to read remote instance metadata for platform health.", exception);
            }
        }

        if (lastKnownRemotes.TryGetValue(remote.InstanceId, out var lastKnown))
        {
            return lastKnown with { Health = "unavailable", ObservedAt = observedAt };
        }

        return new PlatformHealthInstance
        {
            Id = remote.InstanceId,
            Name = new Uri(remote.BaseAddress).Host,
            ApiUrl = remote.BaseAddress.TrimEnd('/') + "/api/",
            ObservedAt = observedAt
        };
    }

    async Task<RemoteConfiguration[]> GetRemoteConfigurations(CancellationToken cancellationToken)
    {
        if (settings.RemoteInstances.Length == 0)
        {
            return [];
        }

        try
        {
            return await configurationApi.GetRemoteConfigs(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            log.Warn("Unable to refresh remote configurations for platform health.", exception);
            return [];
        }
    }

    readonly ConcurrentDictionary<string, PlatformHealthInstance> lastKnownRemotes = new(StringComparer.Ordinal);
    static readonly ILog log = LogManager.GetLogger<PlatformHealthApi>();

    sealed class InstanceConfiguration
    {
        public string InstanceType { get; init; }
        public bool? HealthChecksEnabled { get; init; }
        public HostConfiguration Host { get; init; }
        public RetentionConfiguration DataRetention { get; init; }
        public TransportConfiguration Transport { get; init; }
    }

    sealed class HostConfiguration
    {
        public string InstanceName { get; init; }
        public Guid? HostId { get; init; }
    }

    sealed class RetentionConfiguration
    {
        public TimeSpan? ErrorRetentionPeriod { get; init; }
        public TimeSpan? AuditRetentionPeriod { get; init; }
    }

    sealed class TransportConfiguration
    {
        public string TransportType { get; init; }
        public string ErrorQueue { get; init; }
        public string ErrorLogQueue { get; init; }
        public bool? ForwardErrorMessages { get; init; }
        public string AuditQueue { get; init; }
        public string AuditLogQueue { get; init; }
        public bool? ForwardAuditMessages { get; init; }
    }
}