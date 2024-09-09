namespace ServiceControl.Monitoring.UnitTests.Infrastructure
{
    using System;
    using Microsoft.Extensions.Time.Testing;
    using Monitoring.Infrastructure;
    using NUnit.Framework;

    public class EndpointInstanceActivityTrackerTests
    {
        FakeTimeProvider fakeTimeProvider;
        EndpointInstanceActivityTracker tracker;
        static readonly EndpointInstanceId A1 = new("EndpointA", "instance1");

        [SetUp]
        public void Setup()
        {
            var settings = new Settings { EndpointUptimeGracePeriod = TimeSpan.FromMinutes(5) };
            fakeTimeProvider = new FakeTimeProvider();
            tracker = new EndpointInstanceActivityTracker(settings, fakeTimeProvider);
        }

        [Test]
        public void When_endpoint_instance_is_active()
        {
            tracker.Record(A1, fakeTimeProvider.GetUtcNow().UtcDateTime);

            fakeTimeProvider.Advance(tracker.ExpiredThreshold - TimeSpan.FromMinutes(1));
            Assert.That(tracker.IsExpired(A1), Is.False);
        }

        [Test]
        public void When_endpoint_instance_is_inactive_for_longer_than_grace_period()
        {
            tracker.Record(A1, fakeTimeProvider.GetUtcNow().UtcDateTime);

            fakeTimeProvider.Advance(tracker.ExpiredThreshold);
            fakeTimeProvider.Advance(TimeSpan.FromMinutes(1));

            Assert.That(tracker.IsExpired(A1), Is.True);
        }

        [Test]
        public void When_no_endpoint_instance_reports_stale()
        {
            Assert.That(tracker.IsStale(A1), Is.True);
        }

        [Test]
        public void When_endpoint_reported_within_staleness_period()
        {
            var now = fakeTimeProvider.GetUtcNow().UtcDateTime;

            tracker.Record(A1, now);
            Assert.That(tracker.IsStale(A1), Is.False);
        }

        [Test]
        public void When_endpoint_not_reported_for_longer_than_staleness_period()
        {
            var now = fakeTimeProvider.GetUtcNow().UtcDateTime
                .Subtract(tracker.StalenessThreshold)
                .Subtract(TimeSpan.FromSeconds(1));

            tracker.Record(A1, now);
            Assert.That(tracker.IsStale(A1), Is.True);
        }

        [Test]
        public void When_endpoint_not_reported_for_longer_than_staleness_period_and_reporter_again()
        {
            var now = fakeTimeProvider.GetUtcNow().UtcDateTime
                .Subtract(tracker.StalenessThreshold)
                .Subtract(TimeSpan.FromSeconds(1));

            tracker.Record(A1, now);
            tracker.Record(A1, fakeTimeProvider.GetUtcNow().UtcDateTime);
            Assert.That(tracker.IsStale(A1), Is.False);
        }
    }
}