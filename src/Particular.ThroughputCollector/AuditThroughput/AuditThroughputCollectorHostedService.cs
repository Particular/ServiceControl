namespace Particular.ThroughputCollector.Audit;

using Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Particular.ThroughputCollector.AuditThroughput;
using Particular.ThroughputCollector.Infrastructure;
using Particular.ThroughputCollector.Shared;
using Persistence;
using ServiceControl.Api;

class AuditThroughputCollectorHostedService(
    ILogger<AuditThroughputCollectorHostedService> logger,
    ThroughputSettings throughputSettings,
    IThroughputDataStore dataStore,
    IConfigurationApi configurationApi,
    IEndpointsApi endpointsApi,
    IAuditCountApi auditCountApi,
    TimeProvider timeProvider) : BackgroundService
{

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting AuditThroughputCollector Service");

        backgroundTimer = timeProvider.CreateTimer(async _ => await GatherThroughput(stoppingToken), null, TimeSpan.FromSeconds(30), TimeSpan.FromDays(1));
        stoppingToken.Register(_ => backgroundTimer?.Dispose(), null);

        return Task.CompletedTask;
    }

    async Task GatherThroughput(CancellationToken cancellationToken)
    {
        var utcYesterday = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime).AddDays(-1);
        logger.LogInformation($"Gathering throughput from audit for {utcYesterday.ToShortDateString}");

        try
        {
            await VerifyAuditInstances(cancellationToken);

            var knownEndpoints = await AuditCommands.GetKnownEndpoints(endpointsApi);
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
        catch (Exception ex)
        {
            logger.LogError(ex, "There was a problem getting data from ServiceControl");
        }
    }

    Endpoint ConvertToEndpoint(ServiceControlEndpoint scEndpoint, IEnumerable<AuditCount> auditCounts) =>
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

    private ITimer? backgroundTimer;
}
