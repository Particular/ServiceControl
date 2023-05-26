namespace ServiceControl.Monitoring.UnitTests.Infrastructure
{
    using System;
    using Monitoring.Infrastructure;
    using NUnit.Framework;

    [TestFixture]
    public class StaleEndpointInstanceRemoverTests
    {
        [SetUp]
        public void Setup()
        {
            settings = new Settings
            {
                EndpointUptimeGracePeriod = TimeSpan.FromSeconds(10),
                StaleEndpointInstanceRemovalTimespan = TimeSpan.FromSeconds(10)
            };

            registry = new EndpointRegistry();
            activity = new EndpointInstanceActivityTracker(settings);
        }

        [Test]
        public void Does_Not_Remove_Endpoints_That_Are_Not_Stale()
        {
            var instanceA = new EndpointInstanceId("EndpointA", "instance1");
            var instanceB = new EndpointInstanceId("EndpointB", "instance2");

            registry.Record(instanceA);
            registry.Record(instanceB);

            activity.Record(instanceA, DateTime.UtcNow);
            activity.Record(instanceB, DateTime.UtcNow);

            var remover = new StaleEndpointInstanceRemover(settings, registry, activity);
            remover.CheckStaleEndpoints();

            var instances = registry.GetGroupedByEndpointName().Keys;

            CollectionAssert.AreEquivalent(new[] { instanceA.EndpointName, instanceB.EndpointName }, instances);
        }

        [Test]
        public void Removes_Endpoints_That_Are_Stale()
        {
            var instanceA = new EndpointInstanceId("EndpointA", "instance1");
            var instanceB = new EndpointInstanceId("EndpointB", "instance2");

            registry.Record(instanceA);
            registry.Record(instanceB);

            activity.Record(instanceA, DateTime.UtcNow.AddMinutes(-1));
            activity.Record(instanceB, DateTime.UtcNow);

            var remover = new StaleEndpointInstanceRemover(settings, registry, activity);
            remover.CheckStaleEndpoints();

            var instances = registry.GetGroupedByEndpointName().Keys;

            CollectionAssert.AreEquivalent(new[] { instanceB.EndpointName }, instances);
        }

        EndpointRegistry registry;
        EndpointInstanceActivityTracker activity;
        Settings settings;
    }
}