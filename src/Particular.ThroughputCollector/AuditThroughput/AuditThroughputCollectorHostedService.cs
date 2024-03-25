namespace Particular.ThroughputCollector.AuditThroughput;

using Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Persistence;
using ServiceControl.Api;
using Shared;

class AuditThroughputCollectorHostedService(
    ILogger<AuditThroughputCollectorHostedService> logger,
    ThroughputSettings throughputSettings,
    IThroughputDataStore dataStore,
    IConfigurationApi configurationApi,
    IEndpointsApi endpointsApi,
    IAuditCountApi auditCountApi,
    TimeProvider timeProvider) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation($"Starting {nameof(AuditThroughputCollectorHostedService)}");

        await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);

        using PeriodicTimer timer = new(TimeSpan.FromDays(1), timeProvider);

        try
        {
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

        var knownEndpoints = (await AuditCommands.GetKnownEndpoints(endpointsApi)).ToArray();
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

            if (endpoint?.LastCollectedDate == utcYesterday)
            {
                continue;
            }

            var auditCounts = (await AuditCommands.GetAuditCountForEndpoint(auditCountApi, knownEndpointsLookup[endpointId].UrlName)).ToList();

            if (endpoint == null)
            {
                endpoint = ConvertToEndpoint(knownEndpointsLookup[endpointId], auditCounts);
                await dataStore.SaveEndpoint(endpoint, cancellationToken);
            }

            var missingAuditThroughput = auditCounts
                .Where(auditCount => auditCount.UtcDate > endpoint.LastCollectedDate &&
                                     auditCount.UtcDate < DateOnly.FromDateTime(DateTime.UtcNow))
                .Select(auditCount => new EndpointThroughput
                {
                    DateUTC = auditCount.UtcDate,
                    TotalThroughput = auditCount.Count
                });

            await dataStore.RecordEndpointThroughput(endpoint.Id, missingAuditThroughput, cancellationToken);
        }
    }

    Endpoint ConvertToEndpoint(ServiceControlEndpoint scEndpoint, List<AuditCount> auditCounts) =>
        new(scEndpoint.Name, ThroughputSource.Audit)
        {
            SanitizedName = EndpointNameSanitizer.SanitizeEndpointName(scEndpoint.Name, throughputSettings.Broker),
            EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()],
            DailyThroughput = auditCounts.Any()
                            ? auditCounts.Select(c => new EndpointThroughput { DateUTC = c.UtcDate, TotalThroughput = c.Count }).ToList()
                            : []
        };

    async Task VerifyAuditInstances(CancellationToken cancellationToken)
    {
        var remotesInfo = await AuditCommands.GetAuditRemotes(configurationApi, cancellationToken);

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

        var allHaveAuditCounts = remotesInfo.All(AuditCommands.ValidRemoteInstances);
        if (!allHaveAuditCounts)
        {
            logger.LogWarning($"At least one ServiceControl Audit instance is either not running the required version ({AuditCommands.MinAuditCountsVersion}) or is not configured for at least 2 days of retention. Audit throughput will not be available.");
        }
    }
}