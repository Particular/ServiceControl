namespace ServiceControl.UnitTests.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Monitoring;

    class EndpointInstanceMonitoringTests
    {
        [Test]
        public async Task When_endpoint_removed_should_stay_removed()
        {
            var monitor = new EndpointInstanceMonitoring(new FakeDomainEvents());

            var monitoredEndpoint = new EndpointInstanceId("MonitoredEndpoint", "HostName", Guid.NewGuid());
            var lastHeartbeat = DateTime.UtcNow;

            monitor.RecordHeartbeat(monitoredEndpoint, lastHeartbeat);
            await monitor.CheckEndpoints(lastHeartbeat);

            Assert.IsTrue(monitor.HasEndpoint(monitoredEndpoint.UniqueId), "Monitored Endpoint should be recorded");

            monitor.RemoveEndpoint(monitoredEndpoint.UniqueId);

            Assert.IsFalse(monitor.HasEndpoint(monitoredEndpoint.UniqueId), "Monitored Endpoint should be removed");

            await monitor.CheckEndpoints(lastHeartbeat);

            Assert.IsFalse(monitor.HasEndpoint(monitoredEndpoint.UniqueId), "Monitored Endpoint should not be added back");
        }

        class FakeDomainEvents : IDomainEvents
        {
            public Task Raise<T>(T domainEvent) where T : IDomainEvent
            {
                return Task.CompletedTask;
            }
        }
    }
}
