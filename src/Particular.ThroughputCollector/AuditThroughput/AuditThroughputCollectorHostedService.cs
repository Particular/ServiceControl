namespace Particular.ThroughputCollector.AuditThroughput;

using System.Threading;
using Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Persistence;
using ServiceControl.Transports;
using Shared;

public class AuditThroughputCollectorHostedService(
    ILogger<AuditThroughputCollectorHostedService> logger,
    ThroughputSettings throughputSettings,
    IThroughputDataStore dataStore,
    IAuditQuery auditQuery,
    TimeProvider timeProvider,
    IBrokerThroughputQuery? brokerThroughputQuery = null) : BackgroundService
{
    public TimeSpan DelayStart { get; set; } = TimeSpan.FromSeconds(40);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation($"Starting {nameof(AuditThroughputCollectorHostedService)}");

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
        catch (OperationCanceledException)
        {
            logger.LogInformation($"Stopping {nameof(AuditThroughputCollectorHostedService)} timer");
        }
    }

    async Task GatherThroughput(CancellationToken cancellationToken)
    {
        var utcYesterday = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime).AddDays(-1);
        logger.LogInformation($"Gathering throughput from audit for {utcYesterday.ToShortDateString}");

        await VerifyAuditInstances(cancellationToken);

        var knownEndpoints = (await auditQuery.GetKnownEndpoints(cancellationToken)).ToArray();
        var knownEndpointsLookup = knownEndpoints
            .ToDictionary(knownEndpoint => new EndpointIdentifier(knownEndpoint.Name, ThroughputSource.Audit));

        if (!knownEndpoints.Any())
        {
            logger.LogWarning("No known endpoints could be found.");
        }

        foreach (var tuple in await dataStore.GetEndpoints(knownEndpointsLookup.Keys, cancellationToken))
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
                .Select(auditCount => new EndpointDailyThroughput(auditCount.UtcDate, auditCount.Count));

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

        if (PlatformEndpointHelper.IsPlatformEndpoint(scEndpoint.Name, throughputSettings))
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
                logger.LogInformation($"ServiceControl Audit instance at {remote.ApiUri} detected running version {remote.SemanticVersion}");
            }
            else
            {
                logger.LogWarning($"Unable to determine the version of one or more ServiceControl Audit instances. For the instance with URI {remote.ApiUri}, the status was '{remote.Status}' and the version string returned was '{remote.VersionString}'.");
            }
        }

        var allHaveAuditCounts = remotesInfo.All(auditQuery.ValidRemoteInstances);
        if (!allHaveAuditCounts)
        {
            logger.LogWarning($"At least one ServiceControl Audit instance is either not running the required version ({auditQuery.MinAuditCountsVersion}) or is not configured for at least 2 days of retention. Audit throughput will not be available.");
        }
    }

    async Task SaveAuditInstanceData(List<RemoteInstanceInformation>? auditRemotes, CancellationToken cancellationToken)
    {
        PlatformEndpointHelper.AuditQueues = auditRemotes?.SelectMany(s => s.Queues)?.ToList() ?? [];

        if (auditRemotes != null)
        {
            var auditRemoteInstances = auditRemotes.Select(s => new AuditInstance
            {
                Url = s.ApiUri ?? "",
                MessageTransport = s.Transport,
                Version = s.VersionString
            });

            await dataStore.SaveAuditInstancesInEnvironmentData(auditRemoteInstances.ToList(), cancellationToken);
        }
    }
}