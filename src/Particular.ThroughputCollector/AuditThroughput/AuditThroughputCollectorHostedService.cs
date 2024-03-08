namespace Particular.ThroughputCollector.Audit
{
    using Contracts;
    using Infrastructure;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Persistence;
    using Shared;

    class AuditThroughputCollectorHostedService : IHostedService
    {
        public AuditThroughputCollectorHostedService(ILoggerFactory loggerFactory, ThroughputSettings throughputSettings, IThroughputDataStore dataStore)
        {
            logger = loggerFactory.CreateLogger<AuditThroughputCollectorHostedService>();
            this.throughputSettings = throughputSettings;
            this.dataStore = dataStore;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting AuditThroughputCollector Service");
            auditThroughputGatherTimer = new Timer(async _ => await GatherThroughput(cancellationToken), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)); //TODO this will change to every hour (or every few hours?)
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Stopping AuditThroughputCollector Service");
            auditThroughputGatherTimer?.Dispose();
            return Task.CompletedTask;
        }

        async Task GatherThroughput(CancellationToken cancellationToken)
        {
            var utcYesterday = DateTime.UtcNow.Date.AddDays(-1);
            logger.LogInformation($"Gathering throughput from audit for {utcYesterday.ToShortDateString}");

            try
            {
                var httpFactory = await HttpAuth.CreateHttpClientFactory(throughputSettings.BrokerSettingValues[ServiceControlSettings.API], logger, configureNewClient: c => c.Timeout = TimeSpan.FromSeconds(30), cancellationToken: cancellationToken);
                var primary = new ServiceControlClient("ServiceControl", throughputSettings.BrokerSettingValues[ServiceControlSettings.API], httpFactory, logger);
                await primary.CheckEndpoint(content => content.Contains("\"known_endpoints_url\"") && content.Contains("\"endpoints_messages_url\""), cancellationToken); //TODO do we need this since we know the SC url?
                var knownEndpoints = await Commands.GetKnownEndpoints(primary, logger, cancellationToken);

                if (!knownEndpoints.Any())
                {
                    throw new HaltException(HaltReason.InvalidEnvironment, "Successfully connected to ServiceControl API but no known endpoints could be found.");
                }

                foreach (var endpoint in knownEndpoints)
                {
                    if (!await ThroughputRecordedForYesterday(endpoint.Name, utcYesterday))
                    {
                        //for each endpoint record the audit count for the day we are currently doing as well as any others that are available
                        await dataStore.RecordEndpointThroughput(SCEndpointToEndpoint(endpoint));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "There was a problem getting data from ServiceControl");
            }
        }

        async Task<bool> ThroughputRecordedForYesterday(string endpointName, DateTime utcDateTime)
        {
            var endpoint = await dataStore.GetEndpointByName(endpointName, ThroughputSource.Audit);

            return endpoint?.DailyThroughput?.Any(a => a.DateUTC == utcDateTime) ?? false;
        }

        Endpoint SCEndpointToEndpoint(ServiceControlEndpoint scEndpoint)
        {
            return new Endpoint
            {
                Name = scEndpoint.Name,
                SanitizedName = EndpointNameSanitizer.SanitizeEndpointName(scEndpoint.Name, throughputSettings.Broker),
                ThroughputSource = ThroughputSource.Audit,
                EndpointIndicators = new string[] { EndpointIndicator.KnownEndpoint.ToString() },
                DailyThroughput = scEndpoint.AuditCounts.Any() ? scEndpoint.AuditCounts.Select(c => new EndpointThroughput { DateUTC = c.UtcDate, TotalThroughput = c.Count }).ToList() : []
            };
        }

        readonly ILogger logger;
        ThroughputSettings throughputSettings;
        Timer? auditThroughputGatherTimer;
        IThroughputDataStore dataStore;
    }
}
