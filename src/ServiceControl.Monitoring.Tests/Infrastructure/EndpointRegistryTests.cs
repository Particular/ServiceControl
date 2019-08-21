namespace ServiceControl.Monitoring.Tests.Infrastructure
{
    using System.Linq;
    using Monitoring.Infrastructure;
    using NUnit.Framework;

    public class EndpointRegistryTests
    {
        [Test]
        public void When_known_endpoint_instance_changes_name_existing_entry_is_used_and_udpated()
        {
            var registry = new EndpointRegistry();

            var originalId = new EndpointInstanceId("LogicalName", "instance-id", "original-name");
            var renamedId = new EndpointInstanceId(originalId.EndpointName, originalId.InstanceId, "renamed-name");

            registry.Record(originalId);
            registry.Record(renamedId);

            var records = registry.GetForEndpointName(originalId.EndpointName).ToArray();

            Assert.AreEqual(1, records.Length, "Existing entry should be reused");
            Assert.AreEqual("renamed-name", records[0].InstanceName);
        }
    }
}