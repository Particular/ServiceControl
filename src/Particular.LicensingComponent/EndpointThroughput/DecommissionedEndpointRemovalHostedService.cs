namespace Particular.LicensingComponent
{
    using System;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Particular.LicensingComponent.Persistence;

    /// <summary>
    /// Removes EndpointDocument/LicensingEndpoint entities that haven't seen throughput for ThroughputReporting.ReportedMonths, 
    /// since these can be considered to be decommissioned
    /// </summary>
    public class DecommissionedEndpointRemovalHostedService(
    ILogger<DecommissionedEndpointRemovalHostedService> logger,
    ILicensingDataStore dataStore,
    IThroughputCollector throughputCollector,
    TimeProvider timeProvider)
    : BackgroundService
    {
        // This doesn't need to be run too often, since the difference between ThroughputReporting.ReportedMonths and
        // ThroughputReporting.ReportedMonths - 1day is insignificant when determining whether an endpoint should be 
        // considered decommissioned or not
        const int timerPeriodDays = 1;

        protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Starting {ServiceName}", nameof(DecommissionedEndpointRemovalHostedService));

            try
            {
                using PeriodicTimer timer = new(TimeSpan.FromDays(timerPeriodDays), timeProvider);

                do
                {
                    try
                    {
                        await foreach (var data in throughputCollector.GetDistinctEndpointThroughput(null, cancellationToken).Where(data => !data.ThroughputData.Any(x => x.Any())))
                        {
                            try
                            {
                                //it's possible that endpoints have been created but haven't yet had their throughput written. Give leeway on CreatedDate to allow for this scenario.
                                //endpoints that were created before the CreatedDate field was introduced are guaranteed to be older and hence have valid throughput
                                var endpointsWithoutThroughput = data.EndpointGroupPerQueue.Where(endpoint => !endpoint.CreatedDate.HasValue || endpoint.CreatedDate.Value <= timeProvider.GetUtcNow().AddDays(-1)).ToArray();
                                if (endpointsWithoutThroughput.Any())
                                {
                                    logger.LogInformation("Removing Endpoint {EndpointName} from throughput due to no recent activity", data.EndpointName);
                                    await dataStore.RemoveEndpoints([.. endpointsWithoutThroughput.Select(endpoint => endpoint.Id)], cancellationToken);
                                }
                            }
                            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, $"Error encountered during decomissioned endpoint cleanup for endpoint {data.EndpointName}. Will try again in {timerPeriodDays} days.");
                            }
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"Error encountered during decomissioned endpoint cleanup. Will try again in {timerPeriodDays} days.");
                    }
                } while (await timer.WaitForNextTickAsync(cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Stopping {ServiceName}", nameof(DecommissionedEndpointRemovalHostedService));
            }
        }
    }
}
