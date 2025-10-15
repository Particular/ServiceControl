namespace Particular.LicensingComponent.AuditThroughput;

using Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Persistence;
using ServiceControl.Transports.BrokerThroughput;
using Shared;

public class AuditThroughputCollectorHostedService(
    ILogger<AuditThroughputCollectorHostedService> logger,
    ThroughputSettings throughputSettings,
    ILicensingDataStore dataStore,
    IAuditQuery auditQuery,
    TimeProvider timeProvider,
    PlatformEndpointHelper platformEndpointHelper,
    IBrokerThroughputQuery? brokerThroughputQuery = null
    ) : BackgroundService
{
    public TimeSpan DelayStart { get; set; } = TimeSpan.FromSeconds(40);
    public static List<string> AuditQueues { get; set; } = [];

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting {ServiceName}", nameof(AuditThroughputCollectorHostedService));

        try
        {
            await Task.Delay(DelayStart, cancellationToken);

            using PeriodicTimer timer = new(TimeSpan.FromDays(1), timeProvider);

            do
            {
                try
                {
                    await GatherThroughput(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to gather throughput from audit");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Stopping {ServiceName}", nameof(AuditThroughputCollectorHostedService));
        }
    }

    async Task GatherThroughput(CancellationToken cancellationToken)
    {
        var utcYesterday = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime).AddDays(-1);
        logger.LogInformation("Gathering throughput from audit for {AuditDate}", utcYesterday.ToShortDateString());

        await VerifyAuditInstances(cancellationToken);

        var knownEndpoints = (await auditQuery.GetKnownEndpoints(cancellationToken)).ToArray();
        var knownEndpointsLookup = knownEndpoints
            .ToDictionary(knownEndpoint => new EndpointIdentifier(knownEndpoint.Name, ThroughputSource.Audit));

        if (!knownEndpoints.Any())
        {
            logger.LogWarning("No known endpoints could be found");
        }

        foreach (var tuple in await dataStore.GetEndpoints([.. knownEndpointsLookup.Keys], cancellationToken))
        {
            var endpointId = tuple.Id;
            var endpoint = tuple.Endpoint;

            if (endpoint?.LastCollectedDate >= utcYesterday)
            {
                continue;
            }

            var auditCounts = (await auditQuery.GetAuditCountForEndpoint(knownEndpointsLookup[endpointId].UrlName, cancellationToken)).ToList();

            if (endpoint == null)
            {
                endpoint = ConvertToEndpoint(knownEndpointsLookup[endpointId]);
                await dataStore.SaveEndpoint(endpoint, cancellationToken);
            }

            var missingAuditThroughput = auditCounts
                .Where(auditCount => auditCount.UtcDate > endpoint.LastCollectedDate &&
                                     auditCount.UtcDate < DateOnly.FromDateTime(DateTime.UtcNow))
                .Select(auditCount => new EndpointDailyThroughput(auditCount.UtcDate, auditCount.Count))
                .ToList();

            await dataStore.RecordEndpointThroughput(endpoint.Id.Name, endpoint.Id.ThroughputSource, missingAuditThroughput, cancellationToken);
        }
    }

    Endpoint ConvertToEndpoint(ServiceControlEndpoint scEndpoint)
    {
        var endpoint = new Endpoint(scEndpoint.Name, ThroughputSource.Audit)
        {
            SanitizedName = brokerThroughputQuery != null ? brokerThroughputQuery.SanitizeEndpointName(scEndpoint.Name) : scEndpoint.Name,
            EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()]
        };

        if (platformEndpointHelper.IsPlatformEndpoint(scEndpoint.Name, throughputSettings))
        {
            endpoint.EndpointIndicators.Append(EndpointIndicator.PlatformEndpoint.ToString());
        }

        return endpoint;
    }

    async Task VerifyAuditInstances(CancellationToken cancellationToken)
    {
        var remotesInfo = await auditQuery.GetAuditRemotes(cancellationToken);
        await SaveAuditInstanceData(remotesInfo, cancellationToken);

        foreach (var remote in remotesInfo)
        {
            if (remote.Status == "online" || remote.SemanticVersion is not null)
            {
                logger.LogInformation("ServiceControl Audit instance at {RemoteApiUri} detected running version {RemoteSemanticVersion}", remote.ApiUri, remote.SemanticVersion);
            }
            else
            {
                logger.LogWarning("Unable to determine the version of one or more ServiceControl Audit instances. For the instance with URI {RemoteApiUri}, the status was '{RemoteStatus}' and the version string returned was '{RemoteVersionString}'", remote.ApiUri, remote.Status, remote.VersionString);
            }
        }

        var allHaveAuditCounts = remotesInfo.All(auditQuery.ValidRemoteInstances);
        if (!allHaveAuditCounts)
        {
            logger.LogWarning("At least one ServiceControl Audit instance is either not running the required version ({RequiredAuditVersion}) or is not configured for at least 2 days of retention. Audit throughput will not be available", auditQuery.MinAuditCountsVersion);
        }
    }

    async Task SaveAuditInstanceData(List<RemoteInstanceInformation>? auditRemotes, CancellationToken cancellationToken)
    {
        AuditQueues = auditRemotes?.SelectMany(s => s.Queues)?.ToList() ?? [];

        if (auditRemotes != null)
        {
            var versions = auditRemotes
                .Where(s => s.VersionString is not null)
                .GroupBy(s => s.VersionString!)
                .ToDictionary(g => g.Key, g => g.Count());

            var transports = auditRemotes
                .Where(s => s.Transport is not null)
                .GroupBy(s => s.Transport!)
                .ToDictionary(g => g.Key, g => g.Count());

            await dataStore.SaveAuditServiceMetadata(new AuditServiceMetadata(versions, transports), cancellationToken);
        }
    }
}