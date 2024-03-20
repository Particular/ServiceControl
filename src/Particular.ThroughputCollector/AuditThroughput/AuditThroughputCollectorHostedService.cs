namespace Particular.ThroughputCollector.Audit
{
    using Contracts;
    using Infrastructure;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
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

                var knownEndpoints = await GetKnownEndpoints();

                if (!knownEndpoints.Any())
                {
                    logger.LogWarning("Successfully connected to ServiceControl API but no known endpoints could be found.");
                }

                foreach (var endpoint in knownEndpoints)
                {
                    if (!await ThroughputRecordedForYesterday(endpoint.Name, utcYesterday))
                    {
                        var auditCounts = await GetAuditCountForEndpoint(endpoint.UrlName);
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

        async Task<ServiceControlEndpoint[]> GetKnownEndpoints()
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

            return scEndpoints ?? Array.Empty<ServiceControlEndpoint>();
        }

        async Task<List<AuditCount>> GetAuditCountForEndpoint(string endpointUrlName)
        {
            return (await auditCountApi.GetEndpointAuditCounts(endpointUrlName)).Select(s =>
            {
                return new AuditCount { Count = s.Count, UtcDate = DateOnly.FromDateTime(s.UtcDate) };
            }).ToList();
        }

        async Task VerifyAuditInstances()
        {
            // Verify audit instances also have audit counts
            var remotes = await configurationApi.GetRemoteConfigs();

            var remotesInfo = remotes.Select(configuration => new RemoteInstanceInformation
            {
                ApiUri = configuration.ApiUri,
                VersionString = configuration.Version,
                Status = configuration.Status,
                Retention = configuration.Configuration.DataRetention.AuditRetentionPeriod,
                SemVer = SemVerVersion.TryParse(configuration.Version, out var v) ? v : null
            }).ToArray();

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

            // Want 2d audit retention so we get one complete UTC day no matter what time it is.
            // Customers are expected to run at least version 4.29 for their Audit instances
            var allHaveAuditCounts = remotesInfo.All(r => r.SemVer?.Version >= MinAuditCountsVersion && r.Retention >= TimeSpan.FromDays(2));
            if (!allHaveAuditCounts)
            {
                logger.LogWarning($"At least one ServiceControl Audit instance is either not running the required version ({MinAuditCountsVersion}) or is not configured for at least 2 days of retention. Audit throughput will not be available.");
            }
        }

        readonly ILogger logger;
        ThroughputSettings throughputSettings;
        IThroughputDataStore dataStore;
        IConfigurationApi configurationApi;
        IAuditCountApi auditCountApi;
        IEndpointsApi endpointsApi;

        static readonly Version MinAuditCountsVersion = new Version(4, 29);
    }
}
