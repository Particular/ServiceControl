namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Metrics;
    using ServiceControl.Monitoring.Infrastructure.Api;

    class ReportThroughputHostedService(ILogger<ReportThroughputHostedService> logger, IMessageSession session, IEndpointMetricsApi endpointMetricsApi, TimeProvider timeProvider) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation($"Starting {nameof(ReportThroughputHostedService)}");

            try
            {
                //await Task.Delay(DelayStart, cancellationToken);

                using PeriodicTimer timer = new(TimeSpan.FromMinutes(throughputMinutes), timeProvider);

                do
                {
                    try
                    {
                        await ReportOnThroughput(cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, $"Error obtaining throughput from Monitoring for {throughputMinutes} minutes interval.");
                    }
                } while (await timer.WaitForNextTickAsync(cancellationToken));
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation($"Stopping {nameof(ReportThroughputHostedService)} timer");
            }
        }

        async Task ReportOnThroughput(CancellationToken cancellationToken)
        {
            var endpointData = endpointMetricsApi.GetAllEndpointsMetrics(throughputMinutes);

            if (endpointData.Length > 0)
            {
                var throughputData = new RecordEndpointThroughputData
                {
                    EndDateTime = DateTime.UtcNow,
                    StartDateTime = DateTime.UtcNow.AddMinutes(throughputMinutes),
                    EndpointThroughputData = new EndpointThroughputData[endpointData.Length]
                };

                for (int i = 0; i < endpointData.Length; i++)
                {
                    var average = endpointData[i].Metrics["Throughput"]?.Average ?? 0;
                    throughputData.EndpointThroughputData[i] = new EndpointThroughputData { Name = endpointData[i].Name, Throughput = Convert.ToInt64(average * throughputMinutes * 60) };
                }

                await session.Send(throughputData, cancellationToken);
            }
        }

        static int throughputMinutes = 5;
    }
}