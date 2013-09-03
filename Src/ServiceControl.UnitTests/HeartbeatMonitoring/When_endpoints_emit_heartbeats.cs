namespace ServiceControl.UnitTests.HeartbeatMonitoring
{
    using System;
    using System.Linq;
    using ServiceControl.HeartbeatMonitoring;
    using NUnit.Framework;

    public class When_endpoints_emit_heartbeats
    {
        [Test]
        public void Should_be_added_to_the_list_of_active_endpoint_instances()
        {
            var monitor = new HeartbeatMonitor();

            monitor.RegisterHeartbeat("MyScaledOutEndpoint", "machineA", DateTime.UtcNow);
            monitor.RegisterHeartbeat("MyScaledOutEndpoint", "machineB", DateTime.UtcNow);
            monitor.RegisterHeartbeat("MyLostEndpoint", "machineA", DateTime.UtcNow - TimeSpan.FromHours(1));

            monitor.RefreshHeartbeatsStatuses();

            var knowEndpointInstances = monitor.CurrentStatus();

            Assert.AreEqual(3, knowEndpointInstances.Count(), "Both endpoints should be registered");

            Assert.NotNull(knowEndpointInstances.SingleOrDefault(s => s.Endpoint == "MyScaledOutEndpoint" && s.Machine == "machineA"));
            Assert.NotNull(knowEndpointInstances.SingleOrDefault(s => s.Endpoint == "MyScaledOutEndpoint" && s.Machine == "machineB"));

            Assert.False(knowEndpointInstances.First(s => s.Endpoint == "MyScaledOutEndpoint").Failing.Value);


            Assert.True(knowEndpointInstances.Single(s => s.Endpoint == "MyLostEndpoint").Failing.Value);
        }
    }
}