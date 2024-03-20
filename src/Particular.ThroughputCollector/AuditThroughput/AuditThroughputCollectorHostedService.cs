namespace Particular.ThroughputCollector.Audit
{
    using Contracts;
    using Infrastructure;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Persistence;
    using ServiceControl.Api;

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
                //var httpFactory = await HttpAuth.CreateHttpClientFactory(throughputSettings.BrokerSettingValues[ServiceControlSettings.API], logger, configureNewClient: c => c.Timeout = TimeSpan.FromSeconds(30), cancellationToken: cancellationToken);
                //var primary = new ServiceControlClient("ServiceControl", throughputSettings.BrokerSettingValues[ServiceControlSettings.API], httpFactory, logger);
                //await primary.CheckEndpoint(content => content.Contains("\"known_endpoints_url\"") && content.Contains("\"endpoints_messages_url\""), cancellationToken); //TODO do we need this since we know the SC url?
                //var knownEndpoints = await ServiceControlCommands.GetKnownEndpoints(primary, logger, cancellationToken);
                var knownEndpoints = await ServiceControlCommands.GetKnownEndpoints(configurationApi, endpointsApi, auditCountApi, logger);

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

        private async Task<bool> ThroughputRecordedForYesterday(string endpointName, DateOnly utcDateTime)
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
                EndpointIndicators = [EndpointIndicator.KnownEndpoint.ToString()],
                DailyThroughput = scEndpoint.AuditCounts.Any() ? scEndpoint.AuditCounts.Select(c => new EndpointThroughput { DateUTC = c.UtcDate, TotalThroughput = c.Count }).ToList() : []
            };
        }

        readonly ILogger logger;
        ThroughputSettings throughputSettings;
        IThroughputDataStore dataStore;
        IConfigurationApi configurationApi;
        IAuditCountApi auditCountApi;
        IEndpointsApi endpointsApi;
    }
}
