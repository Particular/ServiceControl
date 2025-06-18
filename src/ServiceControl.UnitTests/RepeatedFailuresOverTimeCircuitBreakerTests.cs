namespace NServiceBus
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;

    // Ideally the circuit breaker would use a time provider to allow for easier testing but that would require a significant refactor
    // and we want keep the changes to a minimum for now to allow backporting to older versions.
    [TestFixture]
    public class RepeatedFailuresOverTimeCircuitBreakerTests
    {
        [SetUp]
        public void Setup() => LoggerUtil.ActiveLoggers = Loggers.Test;

        [Test]
        public async Task Should_disarm_on_success()
        {
            var armedActionCalled = false;
            var disarmedActionCalled = false;

            var circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker(
                "TestCircuitBreaker",
                TimeSpan.FromMilliseconds(100),
                ex => { },
                LoggerUtil.CreateStaticLogger<RepeatedFailuresOverTimeCircuitBreaker>(),
                () => armedActionCalled = true,
                () => disarmedActionCalled = true,
                TimeSpan.Zero,
                TimeSpan.Zero
            );

            await circuitBreaker.Failure(new Exception("Test Exception"));
            circuitBreaker.Success();

            Assert.That(armedActionCalled, Is.True, "The armed action should be called.");
            Assert.That(disarmedActionCalled, Is.True, "The disarmed action should be called.");
        }

        [Test]
        public async Task Should_rethrow_exception_on_success()
        {
            var circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker(
                "TestCircuitBreaker",
                TimeSpan.FromMilliseconds(100),
                ex => { },
                LoggerUtil.CreateStaticLogger<RepeatedFailuresOverTimeCircuitBreaker>(),
                () => { },
                () => throw new Exception("Exception from disarmed action"),
                timeToWaitWhenTriggered: TimeSpan.Zero,
                timeToWaitWhenArmed: TimeSpan.Zero
            );

            await circuitBreaker.Failure(new Exception("Test Exception"));

            var ex = Assert.Throws<Exception>(() => circuitBreaker.Success());
            Assert.That(ex.Message, Is.EqualTo("Exception from disarmed action"));
        }

        [Test]
        public async Task Should_trigger_after_failure_timeout()
        {
            var triggerActionCalled = false;
            Exception lastTriggerException = null;

            var circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker(
                "TestCircuitBreaker",
                TimeSpan.Zero,
                ex => { triggerActionCalled = true; lastTriggerException = ex; },
                LoggerUtil.CreateStaticLogger<RepeatedFailuresOverTimeCircuitBreaker>(),
                timeToWaitWhenTriggered: TimeSpan.Zero,
                timeToWaitWhenArmed: TimeSpan.FromMilliseconds(100)
            );

            await circuitBreaker.Failure(new Exception("Test Exception"));

            Assert.That(triggerActionCalled, Is.True, "The trigger action should be called after timeout.");
            Assert.That(lastTriggerException, Is.Not.Null, "The exception passed to the trigger action should not be null.");
        }

        [Test]
        public void Should_rethrow_exception_on_failure()
        {
            var circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker(
                "TestCircuitBreaker",
                TimeSpan.FromMilliseconds(100),
                ex => { },
                LoggerUtil.CreateStaticLogger<RepeatedFailuresOverTimeCircuitBreaker>(),
                () => throw new Exception("Exception from armed action"),
                () => { },
                timeToWaitWhenTriggered: TimeSpan.Zero,
                timeToWaitWhenArmed: TimeSpan.Zero
            );

            var ex = Assert.ThrowsAsync<Exception>(async () => await circuitBreaker.Failure(new Exception("Test Exception")));
            Assert.That(ex.Message, Is.EqualTo("Exception from armed action"));
        }

        [Test]
        public async Task Should_delay_after_trigger_failure()
        {
            var timeToWaitWhenTriggered = TimeSpan.FromMilliseconds(50);
            var timeToWaitWhenArmed = TimeSpan.FromMilliseconds(100);

            var circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker(
                "TestCircuitBreaker",
                TimeSpan.Zero,
                _ => { },
                LoggerUtil.CreateStaticLogger<RepeatedFailuresOverTimeCircuitBreaker>(),
                timeToWaitWhenTriggered: timeToWaitWhenTriggered,
                timeToWaitWhenArmed: timeToWaitWhenArmed
            );

            var stopWatch = Stopwatch.StartNew();

            await circuitBreaker.Failure(new Exception("Test Exception"));
            await circuitBreaker.Failure(new Exception("Test Exception After Trigger"));

            stopWatch.Stop();

            Assert.That(stopWatch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(timeToWaitWhenTriggered.Add(timeToWaitWhenArmed).TotalMilliseconds).Within(20), "The circuit breaker should delay after a triggered failure.");
        }

        [Test]
        public async Task Should_not_trigger_if_disarmed_before_timeout()
        {
            var triggerActionCalled = false;

            var circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker(
                "TestCircuitBreaker",
                TimeSpan.FromMilliseconds(100),
                ex => triggerActionCalled = true,
                LoggerUtil.CreateStaticLogger<RepeatedFailuresOverTimeCircuitBreaker>(),
                timeToWaitWhenTriggered: TimeSpan.Zero,
                timeToWaitWhenArmed: TimeSpan.Zero
            );

            await circuitBreaker.Failure(new Exception("Test Exception"));
            circuitBreaker.Success();

            Assert.That(triggerActionCalled, Is.False, "The trigger action should not be called if the circuit breaker was disarmed.");
        }

        [Test]
        public async Task Should_handle_concurrent_failure_and_success()
        {
            var armedActionCalled = false;
            var disarmedActionCalled = false;
            var triggerActionCalled = false;

            var circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker(
                "TestCircuitBreaker",
                TimeSpan.FromMilliseconds(100),
                ex => triggerActionCalled = true,
                LoggerUtil.CreateStaticLogger<RepeatedFailuresOverTimeCircuitBreaker>(),
                () => armedActionCalled = true,
                () => disarmedActionCalled = true,
                TimeSpan.Zero,
                TimeSpan.Zero
            );

            var failureTask = circuitBreaker.Failure(new Exception("Test Exception"));
            var successTask = Task.Run(() =>
            {
                Thread.Sleep(50); // Simulate some delay before success
                circuitBreaker.Success();
            });

            await Task.WhenAll(failureTask, successTask);

            Assert.That(armedActionCalled, Is.True, "The armed action should be called.");
            Assert.That(disarmedActionCalled, Is.True, "The disarmed action should be called.");
            Assert.That(triggerActionCalled, Is.False, "The trigger action should not be called if success occurred before timeout.");
        }

        [Test]
        public async Task Should_handle_high_concurrent_failure_and_success()
        {
            var armedActionCalled = 0;
            var disarmedActionCalled = 0;
            var triggerActionCalled = 0;

            var circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker(
                "TestCircuitBreaker",
                TimeSpan.FromSeconds(5),
                ex => Interlocked.Increment(ref triggerActionCalled),
                LoggerUtil.CreateStaticLogger<RepeatedFailuresOverTimeCircuitBreaker>(),
                () => Interlocked.Increment(ref armedActionCalled),
                () => Interlocked.Increment(ref disarmedActionCalled),
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(25)
            );

            var tasks = Enumerable.Range(0, 1000)
                .Select(
                    i => i % 2 == 0 ?
                    circuitBreaker.Failure(new Exception($"Test Exception {i}")) :
                    Task.Run(() =>
                    {
                        Thread.Sleep(25); // Simulate some delay before success
                        circuitBreaker.Success();
                    })
                ).ToArray();

            await Task.WhenAll(tasks);

            Assert.That(armedActionCalled, Is.EqualTo(1), "The armed action should be called.");
            Assert.That(disarmedActionCalled, Is.EqualTo(1), "The disarmed action should be called.");
            Assert.That(triggerActionCalled, Is.Zero, "The trigger action should not be called if success occurred before timeout.");
        }

        [Test]
        public async Task Should_trigger_after_multiple_failures_and_timeout()
        {
            var triggerActionCalled = false;

            var circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker(
                "TestCircuitBreaker",
                TimeSpan.FromMilliseconds(50),
                ex => triggerActionCalled = true,
                LoggerUtil.CreateStaticLogger<RepeatedFailuresOverTimeCircuitBreaker>(),
                timeToWaitWhenTriggered: TimeSpan.FromMilliseconds(50),
                timeToWaitWhenArmed: TimeSpan.FromMilliseconds(50)
            );

            await circuitBreaker.Failure(new Exception("Test Exception"));
            await circuitBreaker.Failure(new Exception("Another Exception After Trigger"));

            Assert.That(triggerActionCalled, Is.True, "The trigger action should be called after repeated failures and timeout.");
        }
    }
}