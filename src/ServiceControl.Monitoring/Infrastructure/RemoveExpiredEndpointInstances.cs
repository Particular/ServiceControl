namespace ServiceControl.Monitoring;

using System;
using System.Threading;
using System.Threading.Tasks;
using Http.Diagrams;
using Infrastructure;
using Infrastructure.Api;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class RemoveExpiredEndpointInstances(
    ILogger<RemoveExpiredEndpointInstances> logger,
    IEndpointMetricsApi metricsApi,
    EndpointInstanceActivityTracker activityTracker,
    TimeProvider timeProvider) : BackgroundService
{
    const int IntervalInMinutes = 5;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation($"Starting {nameof(RemoveExpiredEndpointInstances)}");

        try
        {
            using PeriodicTimer timer = new(TimeSpan.FromMinutes(IntervalInMinutes), timeProvider);

            do
            {
                try
                {
                    foreach (MonitoredEndpoint monitoredEndpoint in metricsApi.GetAllEndpointsMetrics())
                    {
                        foreach (string endpointInstanceId in monitoredEndpoint.EndpointInstanceIds)
                        {
                            if (activityTracker.IsExpired(new EndpointInstanceId(monitoredEndpoint.Name,
                                    endpointInstanceId)))
                            {
                                metricsApi.DeleteEndpointInstance(monitoredEndpoint.Name, endpointInstanceId);
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex,
                        $"Error deleting expired endpoint instances, trying again in {IntervalInMinutes} minutes.");
                }
            } while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation($"Stopping {nameof(RemoveExpiredEndpointInstances)} timer");
        }
    }
}