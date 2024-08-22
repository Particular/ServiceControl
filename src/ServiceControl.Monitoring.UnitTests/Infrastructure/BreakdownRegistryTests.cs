namespace ServiceControl.Monitoring.UnitTests.Infrastructure
{
    using Monitoring.Infrastructure;
    using NUnit.Framework;

    [TestFixture]
    public class BreakdownRegistryTests
    {
        [SetUp]
        public void Setup()
        {
            registry = new EndpointRegistry();
        }

        [Test]
        public void Filters_endpoint_instances_by_endpoint_name()
        {
            var instanceA = new EndpointInstanceId("EndpointA", "instance1");
            var instanceB = new EndpointInstanceId("EndpointB", "instance2");

            registry.Record(instanceA);
            registry.Record(instanceB);

            var instances = registry.GetForEndpointName("EndpointA");

            Assert.That(instances, Is.EquivalentTo(new[] { instanceA }));
        }

        [Test]
        public void Returns_all_physical_instances_for_logical_endpoint_name()
        {
            var instanceA = new EndpointInstanceId("EndpointA", "instance1");
            var instanceB = new EndpointInstanceId("EndpointB", "instance2");
            var instanceC = new EndpointInstanceId("EndpointA", "instance3");

            registry.Record(instanceA);
            registry.Record(instanceB);
            registry.Record(instanceC);

            var instances = registry.GetForEndpointName("EndpointA");

            Assert.That(instances, Is.EquivalentTo(new[] { instanceA, instanceC }));
        }

        [Test]
        public void Returns_two_different_stored_logical_endpoints()
        {
            var instanceA = new EndpointInstanceId("EndpointA", "instance1");
            var instanceB = new EndpointInstanceId("EndpointB", "instance2");

            registry.Record(instanceA);
            registry.Record(instanceB);

            var endpoints = registry.GetGroupedByEndpointName();

            Assert.That(endpoints.Keys, Is.EquivalentTo(new[] { "EndpointA", "EndpointB" }));
        }

        [Test]
        public void Returns_distinct_endpoint_names()
        {
            var instanceA = new EndpointInstanceId("EndpointA", "instance1");
            var instanceB = new EndpointInstanceId("EndpointA", "instance1");

            registry.Record(instanceA);
            registry.Record(instanceB);

            var endpoints = registry.GetGroupedByEndpointName();

            Assert.That(endpoints.Keys, Is.EquivalentTo(new[] { "EndpointA" }));
        }

        [Test]
        public void Returns_distinct_endpoint_instances()
        {
            var instanceA = new EndpointInstanceId("EndpointA", "instance1");
            var instanceB = new EndpointInstanceId("EndpointA", "instance1");

            registry.Record(instanceA);
            registry.Record(instanceB);

            var instances = registry.GetForEndpointName("EndpointA");

            Assert.That(instances, Is.EquivalentTo(new[] { instanceA }));
        }

        [Test]
        public void Removing_all_instances_removes_the_logical_endpoint()
        {
            var instanceA = new EndpointInstanceId("EndpointA", "instance1");
            var instanceB = new EndpointInstanceId("EndpointA", "instance2");
            var instanceC = new EndpointInstanceId("EndpointA", "instance3");

            registry.Record(instanceA);
            registry.Record(instanceB);
            registry.Record(instanceC);

            registry.RemoveEndpointInstance("EndpointA", "instance1");
            registry.RemoveEndpointInstance("EndpointA", "instance2");
            registry.RemoveEndpointInstance("EndpointA", "instance3");

            var instances = registry.GetForEndpointName("EndpointA");

            Assert.That(instances, Is.Empty);
        }

        [Test]
        public void Returns_only_not_removed_instances()
        {
            var instanceA = new EndpointInstanceId("EndpointA", "instance1");
            var instanceB1 = new EndpointInstanceId("EndpointB", "instance1");
            var instanceB2 = new EndpointInstanceId("EndpointB", "instance2");
            var instanceC = new EndpointInstanceId("EndpointC", "instance2");

            registry.Record(instanceA);
            registry.Record(instanceB1);
            registry.Record(instanceB2);
            registry.Record(instanceC);

            registry.RemoveEndpointInstance("EndpointB", "instance2");

            var instances = registry.GetForEndpointName("EndpointB");

            Assert.That(instances, Is.EquivalentTo(new[] { instanceB1 }));
        }

        EndpointRegistry registry;
    }
}