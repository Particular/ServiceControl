namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Implementation;

    [TestFixture]
    class AuditRetentionRetryDelayTests
    {
        [TestCase(1, 5, 10)]
        [TestCase(2, 10, 20)]
        [TestCase(3, 20, 40)]
        [TestCase(6, 160, 320)]
        public void Doubles_with_each_attempt_and_stays_within_the_jitter_range(int attempt, int minSeconds, int maxSeconds)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(AuditRetention.RetryDelay(attempt, 0), Is.EqualTo(TimeSpan.FromSeconds(minSeconds)));
                Assert.That(AuditRetention.RetryDelay(attempt, 0.5), Is.EqualTo(TimeSpan.FromSeconds((minSeconds + maxSeconds) / 2.0)));
                Assert.That(AuditRetention.RetryDelay(attempt, 0.999999), Is.LessThan(TimeSpan.FromSeconds(maxSeconds)));
            }
        }

        [TestCase(10)]
        [TestCase(100)]
        [TestCase(int.MaxValue)]
        public void Never_exceeds_the_sweep_interval(int attempt) =>
            Assert.That(AuditRetention.RetryDelay(attempt, 0.999999), Is.LessThan(TimeSpan.FromHours(1)).And.GreaterThanOrEqualTo(TimeSpan.FromMinutes(30)));
    }
}
