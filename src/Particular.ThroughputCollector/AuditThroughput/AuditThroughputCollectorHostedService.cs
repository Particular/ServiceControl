namespace Particular.ThroughputCollector.Audit
{
    using System.Threading;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.Infrastructure;
    using Particular.ThroughputCollector.Persistence;
    using Particular.ThroughputCollector.Shared;

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
            auditThroughputGatherTimer = new Timer(async _ => await GatherThroughput(cancellationToken).ConfigureAwait(false), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)); //TODO this will change to every hour (or every few hours?)
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
            DateTime utcDateTime = DateTime.UtcNow.Date;
            logger.LogInformation($"Gathering throughput from audit for ");

            try
            {
                var httpFactory = await HttpAuth.CreateHttpClientFactory(throughputSettings.ServiceControlAPI, logger, configureNewClient: c => c.Timeout = TimeSpan.FromSeconds(30), cancellationToken: cancellationToken).ConfigureAwait(false);
                var primary = new ServiceControlClient("ServiceControl", throughputSettings.ServiceControlAPI, httpFactory, logger);
                await primary.CheckEndpoint(content => content.Contains("\"known_endpoints_url\"") && content.Contains("\"endpoints_messages_url\""), cancellationToken).ConfigureAwait(false); //TODO do we need this since we know the SC url?
                var knownEndpoints = await Commands.GetKnownEndpoints(primary, logger, cancellationToken).ConfigureAwait(false);

                if (!knownEndpoints.Any())
                {
                    throw new HaltException(HaltReason.InvalidEnvironment, "Successfully connected to ServiceControl API but no known endpoints could be found.");
                }

                foreach (var endpoint in knownEndpoints)
                {
                    if (!await ThroughputRecordedForYesterday(endpoint.Name, utcDateTime.AddDays(-1)).ConfigureAwait(false))
                    {
                        //TODO for each endpoint record the audit count for the day we are currently doing
                        //TODO can we assume that everyone is using the new version of audits hence they have audit counts?
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
            var endpoint = await dataStore.GetEndpointByNameOrQueue(endpointName).ConfigureAwait(false);

            return endpoint?.DailyThroughput?.Any(a => a.DateUTC == utcDateTime) ?? false;
        }

        readonly ILogger logger;
        ThroughputSettings throughputSettings;
        Timer? auditThroughputGatherTimer;
        IThroughputDataStore dataStore;
    }
}
