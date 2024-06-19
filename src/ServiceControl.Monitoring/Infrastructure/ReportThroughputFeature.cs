namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Metrics;
    using ServiceControl.Monitoring.Infrastructure.Api;

    class ReportThroughputFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(b => b.GetRequiredService<ReportThroughputFeatureStartup>());
        }
    }

    class ReportThroughputFeatureStartup(IEndpointMetricsApi endpointMetricsApi, ILogger<ReportThroughputFeatureStartup> logger) : FeatureStartupTask, IDisposable
    {
        public void Dispose()
        {
            monitoringDataTimer?.Dispose();
        }

        protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => monitoringDataTimer = new Timer(async objectstate => { await SendReport(session); }, null, TimeSpan.Zero, TimeSpan.FromMinutes(throughputMinutes)));
        }

        protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        async Task SendReport(IMessageSession session)
        {
            try
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

                    await session.Send(throughputData);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error obtaining throughput from Monitoring for {throughputMinutes} minutes interval.");
            }
        }

        Timer monitoringDataTimer;
        static int throughputMinutes = 5;
    }
}