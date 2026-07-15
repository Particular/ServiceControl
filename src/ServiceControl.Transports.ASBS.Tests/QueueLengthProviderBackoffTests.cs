namespace ServiceControl.Transports.UnitTests.ASBS
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Transports.ASBS;

    [TestFixture]
    class QueueLengthProviderBackoffTests
    {
        // Base is the configured QueueLengthQueryDelayInterval (default). Max is the internal reactive-backoff cap.
        static readonly TimeSpan Base = TimeSpan.FromMilliseconds(500);
        static readonly TimeSpan Max = TimeSpan.FromSeconds(60);

        [Test]
        public void Doubles_the_delay_when_throttled()
        {
            // A throttled cycle backs off so the provider stops making the throttling worse.
            Assert.That(QueueLengthProvider.NextDelay(Base, Base, Max, throttled: true),
                Is.EqualTo(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void Caps_the_delay_at_max_when_throttled()
        {
            // 40s doubled would be 80s; the cap keeps it bounded.
            Assert.That(QueueLengthProvider.NextDelay(TimeSpan.FromSeconds(40), Base, Max, throttled: true),
                Is.EqualTo(Max));
        }

        [Test]
        public void Halves_the_delay_on_success_while_still_backed_off()
        {
            // Success steps the cadence back down gradually rather than snapping to base, avoiding
            // throttle -> reset -> throttle oscillation.
            Assert.That(QueueLengthProvider.NextDelay(TimeSpan.FromSeconds(2), Base, Max, throttled: false),
                Is.EqualTo(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void Never_drops_below_base_on_success()
        {
            Assert.That(QueueLengthProvider.NextDelay(Base, Base, Max, throttled: false),
                Is.EqualTo(Base));
        }

        [Test]
        public void Http_429_is_classified_as_throttling()
        {
            Assert.That(ManagementThrottleDetector.IsThrottleResponse(429), Is.True);
        }

        [TestCase(200)]
        [TestCase(404)]
        [TestCase(500)]
        [TestCase(503)]
        public void Non_429_responses_are_not_classified_as_throttling(int statusCode)
        {
            Assert.That(ManagementThrottleDetector.IsThrottleResponse(statusCode), Is.False);
        }
    }
}
