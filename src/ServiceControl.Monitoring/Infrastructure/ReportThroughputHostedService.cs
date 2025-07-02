namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Metrics;
    using NServiceBus.Unicast.Queuing;
    using ServiceControl.Monitoring.Infrastructure.Api;
    using ServiceControl.Transports;

    class ReportThroughputHostedService(ILogger<ReportThroughputHostedService> logger, IMessageSession session, IEndpointMetricsApi endpointMetricsApi, Settings settings, TimeProvider timeProvider, ITransportCustomization transportCustomization) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation($"Starting {nameof(ReportThroughputHostedService)}");

            var serviceControlThroughputDataQueue = transportCustomization.ToTransportQualifiedQueueName(settings.ServiceControlThroughputDataQueue);

            try
            {
                using PeriodicTimer timer = new(TimeSpan.FromMinutes(ReportSendingIntervalInMinutes), timeProvider);

                do
                {
                    try
                    {
                        await ReportOnThroughput(serviceControlThroughputDataQueue, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        if (ex.InnerException is not null and QueueNotFoundException)
                        {
                            logger.LogError($"Error obtaining throughput from Monitoring for {ReportSendingIntervalInMinutes} minutes interval: {ex?.InnerException?.Message}");
                        }
                        else
                        {
                            logger.LogError(ex, $"Error obtaining throughput from Monitoring for {ReportSendingIntervalInMinutes} minutes interval");
                        }
                    }
                } while (await timer.WaitForNextTickAsync(cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation($"Stopping {nameof(ReportThroughputHostedService)} timer");
            }
        }

        async Task ReportOnThroughput(string serviceControlThroughputDataQueue, CancellationToken cancellationToken)
        {
            var endpointData = endpointMetricsApi.GetAllEndpointsMetrics(ReportSendingIntervalInMinutes);

            if (endpointData.Length > 0)
            {
                var throughputData = new RecordEndpointThroughputData
                {
                    EndDateTime = DateTime.UtcNow,
                    StartDateTime = DateTime.UtcNow.AddMinutes(-ReportSendingIntervalInMinutes),
                    EndpointThroughputData = new EndpointThroughputData[endpointData.Length]
                };

                for (int i = 0; i < endpointData.Length; i++)
                {
                    var average = endpointData[i].Metrics["Throughput"]?.Average ?? 0;
                    throughputData.EndpointThroughputData[i] = new EndpointThroughputData
                    {
                        Name = endpointData[i].Name,
                        Throughput = Convert.ToInt64(average * ReportSendingIntervalInMinutes * 60)
                    };
                }

                await session.Send(serviceControlThroughputDataQueue, throughputData, cancellationToken);
            }
        }

        const int ReportSendingIntervalInMinutes = 5;
    }
}