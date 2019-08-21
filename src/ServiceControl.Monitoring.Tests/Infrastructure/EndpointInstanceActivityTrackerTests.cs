namespace ServiceControl.Monitoring.Tests.Infrastructure
{
    using System;
    using Monitoring.Infrastructure;
    using NUnit.Framework;

    public class EndpointInstanceActivityTrackerTests
    {
        EndpointInstanceActivityTracker tracker;
        static readonly EndpointInstanceId A1 = new EndpointInstanceId("EndpointA", "instance1");

        [SetUp]
        public void Setup()
        {
            var settings = new Settings() { EndpointUptimeGracePeriod = TimeSpan.FromMinutes(5) };
            tracker = new EndpointInstanceActivityTracker(settings);
        }

        [Test]
        public void When_no_endpoint_instance_reports_not_stale()
        {
            Assert.IsFalse(tracker.IsStale(A1));
        }

        [Test]
        public void When_endpoint_reported_within_staleness_period()
        {
            var now = DateTime.UtcNow;

            tracker.Record(A1, now);
            Assert.IsFalse(tracker.IsStale(A1));
        }

        [Test]
        public void When_endpoint_not_reported_for_longer_than_staleness_period()
        {
            var now = DateTime.UtcNow
                .Subtract(tracker.StalenessThreshold)
                .Subtract(TimeSpan.FromSeconds(1));

            tracker.Record(A1, now);
            Assert.IsTrue(tracker.IsStale(A1));
        }

        [Test]
        public void When_endpoint_not_reported_for_longer_than_staleness_period_and_reporter_again()
        {
            var now = DateTime.UtcNow
                .Subtract(tracker.StalenessThreshold)
                .Subtract(TimeSpan.FromSeconds(1));

            tracker.Record(A1, now);
            tracker.Record(A1, DateTime.UtcNow);
            Assert.IsFalse(tracker.IsStale(A1));
        }
    }
}