namespace ServiceControl.UnitTests.HeartbeatMonitoring
{
    using System;
    using System.Linq;
    using System.Threading;
    using ServiceControl.HeartbeatMonitoring;
    using NUnit.Framework;

    public class When_endpoints_stops_emitting_heartbeats
    {
        [Test]
        public void Should_be_marked_as_inactive_after_the_grace_period_elapses()
        {
            var monitor = new HeartbeatMonitor
                {
                    GracePeriod = TimeSpan.FromSeconds(1)
                };

            monitor.RegisterHeartbeat("MyEndpoint", "machineA", DateTime.UtcNow );

            monitor.RefreshHeartbeatsStatuses();

            Assert.False(monitor.CurrentStatus().Single().Failing.Value, "Endpoint should be ok when first registered");

            Thread.Sleep(1010);

            monitor.RefreshHeartbeatsStatuses();

            Assert.True(monitor.CurrentStatus().Single().Failing.Value, "Endpoint should be marked as failing when grace period is over");
        }
    }
}