namespace ServiceControl.UnitTests.Monitoring
{
    using System;
    using System.Text.Json;
    using NUnit.Framework;
    using Plugin.Heartbeat.Messages;
    using ServiceControl.Monitoring.HeartbeatMonitoring;

    [TestFixture]
    public class HeartbeatTypesArrayToInstanceConverterTests
    {
        JsonSerializerOptions options;

        [SetUp]
        public void Setup() =>
            options = new JsonSerializerOptions
            {
                Converters =
                {
                    new HeartbeatTypesArrayToInstanceConverter()
                },
                TypeInfoResolverChain =
                {
                    HeartbeatSerializationContext.Default
                }
            };

        [Test]
        public void Should_deserialize_heartbeat_arrays()
        {
            var heartbeat = JsonSerializer.Deserialize<EndpointHeartbeat>(@"[
  {
    ""$type"": ""ServiceControl.Plugin.Heartbeat.Messages.EndpointHeartbeat, ServiceControl"",
    ""ExecutedAt"": ""2024-06-02T12:03:41.780"",
    ""EndpointName"": ""Test"",
    ""HostId"": ""1865830e-71b0-dc6c-e146-62cdd0034e6e"",
    ""Host"": ""Machine""
  }
]", options);

            Assert.IsNotNull(heartbeat);
            Assert.AreEqual("Test", heartbeat.EndpointName);
            Assert.AreEqual("Machine", heartbeat.Host);
            Assert.AreEqual(new DateTime(2024, 6, 2, 12, 3, 41, 780, System.DateTimeKind.Utc), heartbeat.ExecutedAt);
            Assert.AreEqual(new Guid("1865830e-71b0-dc6c-e146-62cdd0034e6e"), heartbeat.HostId);
        }

        [Test]
        public void Should_deserialize_single_heartbeat()
        {
            var heartbeat = JsonSerializer.Deserialize<EndpointHeartbeat>(@"{
    ""$type"": ""ServiceControl.Plugin.Heartbeat.Messages.EndpointHeartbeat, ServiceControl"",
    ""ExecutedAt"": ""2024-06-02T12:03:41.780"",
    ""EndpointName"": ""Test"",
    ""HostId"": ""1865830e-71b0-dc6c-e146-62cdd0034e6e"",
    ""Host"": ""Machine""
  }", options);

            Assert.IsNotNull(heartbeat);
            Assert.AreEqual("Test", heartbeat.EndpointName);
            Assert.AreEqual("Machine", heartbeat.Host);
            Assert.AreEqual(new DateTime(2024, 6, 2, 12, 3, 41, 780, System.DateTimeKind.Utc), heartbeat.ExecutedAt);
            Assert.AreEqual(new Guid("1865830e-71b0-dc6c-e146-62cdd0034e6e"), heartbeat.HostId);
        }
    }
}