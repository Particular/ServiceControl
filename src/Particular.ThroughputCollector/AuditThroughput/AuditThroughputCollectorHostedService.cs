namespace Particular.ThroughputCollector.Audit;

using Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Particular.ThroughputCollector.AuditThroughput;
using Particular.ThroughputCollector.Infrastructure;
using Particular.ThroughputCollector.Shared;
using Persistence;
using ServiceControl.Api;

class AuditThroughputCollectorHostedService : BackgroundService
{
    public AuditThroughputCollectorHostedService(
        ILogger<AuditThroughputCollectorHostedService> logger,
        ThroughputSettings throughputSettings,
        IThroughputDataStore dataStore,
        IConfigurationApi configurationApi,
        IEndpointsApi endpointsApi,
        IAuditCountApi auditCountApi,
        TimeProvider timeProvider)
    {
        this.logger = logger;
        this.throughputSettings = throughputSettings;
        this.dataStore = dataStore;
        this.configurationApi = configurationApi;
        this.endpointsApi = endpointsApi;
        this.auditCountApi = auditCountApi;
        this.timeProvider = timeProvider;
        backgroundTimer = timeProvider.CreateTimer(async _ => await GatherThroughput(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting AuditThroughputCollector Service");

        var tcs = new TaskCompletionSource();

        cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cancellationTokenSource.Token.Register(_ => tcs.SetResult(), null);

        backgroundTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromDays(1));

        await tcs.Task;
    }

    async Task GatherThroughput()
    {
        var cancellationToken = cancellationTokenSource.Token;

        var utcYesterday = DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime).AddDays(-1);
        logger.LogInformation($"Gathering throughput from audit for {utcYesterday.ToShortDateString()}");

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
        catch (OperationCanceledException)
        {
            logger.LogInformation("Canceled gathering audit throughput");
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

    public override void Dispose()
    {
        backgroundTimer.Dispose();
        base.Dispose();
    }

    readonly ILogger<AuditThroughputCollectorHostedService> logger;
    readonly ThroughputSettings throughputSettings;
    readonly IThroughputDataStore dataStore;
    readonly IConfigurationApi configurationApi;
    readonly IEndpointsApi endpointsApi;
    readonly IAuditCountApi auditCountApi;
    readonly TimeProvider timeProvider;
    readonly ITimer backgroundTimer;
    CancellationTokenSource cancellationTokenSource = new();
}
