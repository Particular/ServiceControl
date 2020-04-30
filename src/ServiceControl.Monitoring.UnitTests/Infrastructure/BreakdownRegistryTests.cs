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

            CollectionAssert.AreEquivalent(new[] {instanceA}, instances);
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

            CollectionAssert.AreEquivalent(new[] {instanceA, instanceC}, instances);
        }

        [Test]
        public void Returns_two_different_stored_logical_endpoints()
        {
            var instanceA = new EndpointInstanceId("EndpointA", "instance1");
            var instanceB = new EndpointInstanceId("EndpointB", "instance2");

            registry.Record(instanceA);
            registry.Record(instanceB);

            var endpoints = registry.GetGroupedByEndpointName();

            CollectionAssert.AreEquivalent(new[] {"EndpointA", "EndpointB"}, endpoints.Keys);
        }

        [Test]
        public void Returns_distinct_endpoint_names()
        {
            var instanceA = new EndpointInstanceId("EndpointA", "instance1");
            var instanceB = new EndpointInstanceId("EndpointA", "instance1");

            registry.Record(instanceA);
            registry.Record(instanceB);

            var endpoints = registry.GetGroupedByEndpointName();

            CollectionAssert.AreEquivalent(new[] {"EndpointA"}, endpoints.Keys);
        }

        [Test]
        public void Returns_distinct_endpoint_instances()
        {
            var instanceA = new EndpointInstanceId("EndpointA", "instance1");
            var instanceB = new EndpointInstanceId("EndpointA", "instance1");

            registry.Record(instanceA);
            registry.Record(instanceB);

            var instances = registry.GetForEndpointName("EndpointA");

            CollectionAssert.AreEquivalent(new[] { instanceA }, instances);
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

            CollectionAssert.IsEmpty(instances);
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

            CollectionAssert.AreEquivalent(new[] {instanceB1}, instances);
        }

        EndpointRegistry registry;
    }
}