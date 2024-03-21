namespace Particular.ThroughputCollector.Audit;

using AuditThroughput;
using Contracts;
using Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Persistence;
using ServiceControl.Api;
using Shared;
using AuditCount = Contracts.AuditCount;
using Endpoint = Contracts.Endpoint;

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

            if (!knownEndpoints.Any())
            {
                logger.LogWarning("Successfully connected to ServiceControl API but no known endpoints could be found.");
            }

            foreach (var endpoint in knownEndpoints)
            {
                if (!await ThroughputRecordedForYesterday(endpoint.Name, utcYesterday))
                {
                    var auditCounts = await AuditCommands.GetAuditCountForEndpoint(auditCountApi, endpoint.UrlName);
                    //for each endpoint record the audit count for the day we are currently doing as well as any others that are available
                    await dataStore.RecordEndpointThroughput(SCEndpointToEndpoint(endpoint, auditCounts));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was a problem getting data from ServiceControl");
        }
    }

    private async Task<bool> ThroughputRecordedForYesterday(string endpointName, DateOnly utcDateTime)
    {
        var endpoint = await dataStore.GetEndpointByName(endpointName, ThroughputSource.Audit);

        return endpoint?.DailyThroughput?.Any(a => a.DateUTC == utcDateTime) ?? false;
    }

    Endpoint SCEndpointToEndpoint(ServiceControlEndpoint scEndpoint, List<AuditCount> auditCounts)
    {
        return new Endpoint(scEndpoint.Name, ThroughputSource.Audit)
        {
            SanitizedName = EndpointNameSanitizer.SanitizeEndpointName(scEndpoint.Name, throughputSettings.Broker),
            EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()],
            DailyThroughput = auditCounts.Any() ? auditCounts.Select(c => new EndpointThroughput { DateUTC = c.UtcDate, TotalThroughput = c.Count }).ToList() : []
        };
    }

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
