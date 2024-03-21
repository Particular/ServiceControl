namespace Particular.ThroughputCollector.Audit
{
    using Contracts;
    using Infrastructure;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.ThroughputCollector.AuditThroughput;
    using Persistence;
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;
    using Shared;
    using AuditCount = Contracts.AuditCount;
    using Endpoint = Contracts.Endpoint;

    class AuditThroughputCollectorHostedService : BackgroundService
    {
        public AuditThroughputCollectorHostedService(ILogger<AuditThroughputCollectorHostedService> logger, ThroughputSettings throughputSettings, IThroughputDataStore dataStore, IConfigurationApi configurationApi, IEndpointsApi endpointsApi, IAuditCountApi auditCountApi)
        {
            this.logger = logger;
            this.throughputSettings = throughputSettings;
            this.dataStore = dataStore;
            this.configurationApi = configurationApi;
            this.auditCountApi = auditCountApi;
            this.endpointsApi = endpointsApi;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Starting AuditThroughputCollector Service");

            await Task.Delay(30000, stoppingToken);

            using PeriodicTimer timer = new(TimeSpan.FromDays(1));

            try
            {
                do
                {
                    await GatherThroughput();
                } while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Stopping AuditThroughputCollector Service");
            }
        }

        async Task GatherThroughput()
        {
            var utcYesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
            logger.LogInformation($"Gathering throughput from audit for {utcYesterday.ToShortDateString}");

            try
            {
                await VerifyAuditInstances();

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
            return new Endpoint
            {
                Name = scEndpoint.Name,
                SanitizedName = EndpointNameSanitizer.SanitizeEndpointName(scEndpoint.Name, throughputSettings.Broker),
                ThroughputSource = ThroughputSource.Audit,
                EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()],
                DailyThroughput = auditCounts.Any() ? auditCounts.Select(c => new EndpointThroughput { DateUTC = c.UtcDate, TotalThroughput = c.Count }).ToList() : []
            };
        }

        async Task VerifyAuditInstances()
        {
            var remotesInfo = await AuditCommands.GetAuditRemotes(configurationApi);

            foreach (var remote in remotesInfo)
            {
                if (remote.Status == RemoteStatus.Online || remote.SemVer is not null)
                {
                    logger.LogInformation($"ServiceControl Audit instance at {remote.ApiUri} detected running version {remote.SemVer}");
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

        readonly ILogger logger;
        ThroughputSettings throughputSettings;
        IThroughputDataStore dataStore;
        IConfigurationApi configurationApi;
        IAuditCountApi auditCountApi;
        IEndpointsApi endpointsApi;
    }
}
