namespace ServiceControl.UnitTests.Monitoring
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Monitoring;
    using ServiceControl.Persistence;

    class EndpointInstanceMonitoringTests
    {
        [Test]
        public async Task When_endpoint_removed_should_stay_removed()
        {
            var monitor = new EndpointInstanceMonitoring(new FakeDomainEvents(), NullLogger<EndpointInstanceMonitor>.Instance);

            var monitoredEndpoint = new EndpointInstanceId("MonitoredEndpoint", "HostName", Guid.NewGuid());
            var lastHeartbeat = DateTime.UtcNow;

            monitor.RecordHeartbeat(monitoredEndpoint, lastHeartbeat);
            await monitor.CheckEndpoints(lastHeartbeat);

            Assert.That(monitor.HasEndpoint(monitoredEndpoint.UniqueId), Is.True, "Monitored Endpoint should be recorded");

            monitor.RemoveEndpoint(monitoredEndpoint.UniqueId);

            Assert.That(monitor.HasEndpoint(monitoredEndpoint.UniqueId), Is.False, "Monitored Endpoint should be removed");

            await monitor.CheckEndpoints(lastHeartbeat);

            Assert.That(monitor.HasEndpoint(monitoredEndpoint.UniqueId), Is.False, "Monitored Endpoint should not be added back");
        }

        class FakeDomainEvents : IDomainEvents
        {
            public Task Raise<T>(T domainEvent, CancellationToken cancellationToken) where T : IDomainEvent
            {
                return Task.CompletedTask;
            }
        }
    }
}
