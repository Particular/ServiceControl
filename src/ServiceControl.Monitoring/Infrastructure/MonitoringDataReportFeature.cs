namespace ServiceControl.Monitoring
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Metrics;
    using ServiceControl.Monitoring.Http.Diagrams;
    using ServiceControl.Monitoring.Infrastructure;

    class MonitoringDataReportFeature : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(b => b.GetRequiredService<MonitoringDataReportFeatureStartup>());
        }
    }

    class MonitoringDataReportFeatureStartup : FeatureStartupTask, IDisposable
    {
        public MonitoringDataReportFeatureStartup(IEnumerable<IProvideBreakdown> breakdownProviders, EndpointRegistry endpointRegistry, EndpointInstanceActivityTracker activityTracker, MessageTypeRegistry messageTypeRegistry)
        {
            this.breakdownProviders = breakdownProviders;
            this.endpointRegistry = endpointRegistry;
            this.activityTracker = activityTracker;
            this.messageTypeRegistry = messageTypeRegistry;
        }

        public void Dispose()
        {
            monitoringDataTimer?.Dispose();
        }

        protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => monitoringDataTimer = new Timer(async objectstate => { await SendReport(session); }, null, TimeSpan.Zero, TimeSpan.FromMinutes(5)));
        }

        protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        async Task SendReport(IMessageSession session)
        {
            var apiController = new DiagramApiController(breakdownProviders, endpointRegistry, activityTracker, messageTypeRegistry);
            var endpointData = apiController.GetAllEndpointsMetrics(5);

            var monitoringData = new RecordEndpointMonitoringData
            {
                EndDateTime = DateTime.UtcNow,
                StartDateTime = DateTime.UtcNow.AddMinutes(-5),
                EndpointMonitoringData = new EndpointMonitoringData[endpointData.Length]
            };

            for (int i = 0; i < endpointData.Length; i++)
            {
                monitoringData.EndpointMonitoringData[i].Name = endpointData[i].Name;
                monitoringData.EndpointMonitoringData[i].EndpointInstanceIds = endpointData[i].EndpointInstanceIds;
                monitoringData.EndpointMonitoringData[i].Metrics = "some metrics"; //TODO
            }

            await session.Send(monitoringData);
        }

        Timer monitoringDataTimer;
        IEnumerable<IProvideBreakdown> breakdownProviders;
        EndpointRegistry endpointRegistry;
        EndpointInstanceActivityTracker activityTracker;
        MessageTypeRegistry messageTypeRegistry;
    }
}