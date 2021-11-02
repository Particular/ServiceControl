namespace NServiceBus.PlatformConnection.UnitTests
{
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Reflection;
    using Configuration.AdvancedExtensibility;
    using NUnit.Framework;
    using Particular.Approvals;
    using Settings;

    [TestFixture]
    class BasicUsage
    {
        [Test]
        public void UpdatesConfiguration()
        {
            var connectionConfig = ServicePlatformConnectionConfiguration.Parse(@"{
    ""auditQueue"": ""myAuditQueue"",
    ""errorQueue"": ""myErrorQueue"",
    ""heartbeats"": {
        ""heartbeatQueue"": ""heartbeatsServiceControlQueue""
    },
    ""customChecks"": {
        ""customChecksQueue"": ""customChecksServiceControlQueue""
    },
    ""sagaAudit"": {
        ""sagaAuditQueue"": ""sagaAuditServiceControlQueue""
    },
    ""metrics"": {
        ""metricsQueue"": ""metricServiceControlQueue"",
        ""interval"": ""00:00:10""
    }
}");

            var endpointConfig = new EndpointConfiguration("SomeEndpoint");

            var settings = endpointConfig.GetSettings();

            var property = typeof(SettingsHolder).GetField("Overrides", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(property, "Overrides property cannot be found");
            var overrides = property.GetValue(settings) as ConcurrentDictionary<string, object>;
            Assert.IsNotNull(overrides);

            var beforeConnectionKeys = overrides.Keys.ToArray();

            endpointConfig.ConnectToServicePlatform(connectionConfig);

            var afterConnectionKeys = overrides.Keys.ToArray();
            var changes = afterConnectionKeys.Except(beforeConnectionKeys);

            Approver.Verify(changes);
        }
    }
}
