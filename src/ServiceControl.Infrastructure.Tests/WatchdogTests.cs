﻿namespace ServiceControl.Infrastructure.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;

    [TestFixture]
    public class WatchdogTests
    {
        static ILogger log = LoggerUtil.CreateStaticLogger<WatchdogTests>();

        [Test]
        public async Task It_shuts_down_gracefully()
        {
            var started = new TaskCompletionSource<bool>();
            var stopped = new TaskCompletionSource<bool>();

            var dog = new Watchdog("test process", token =>
            {
                started.SetResult(true);
                return Task.CompletedTask;
            }, token =>
            {
                stopped.SetResult(true);
                return Task.CompletedTask;
            }, x => { }, () => { }, TimeSpan.FromSeconds(1), log);

            await dog.Start(() => { }, CancellationToken.None);

            await started.Task;

            await dog.Stop(TestContext.CurrentContext.CancellationToken);

            await stopped.Task;
        }

        [Test]
        public async Task When_stop_fails_stop_should_throw_identifying_ungraceful_stop()
        {
            string lastFailure = null;
            var started = new TaskCompletionSource<bool>();

            var dog = new Watchdog("test process", token =>
            {
                started.SetResult(true);
                return Task.CompletedTask;
            }, token => throw new Exception("Simulated"), x => lastFailure = x, () => lastFailure = null, TimeSpan.FromSeconds(1), log);

            await dog.Start(() => { }, CancellationToken.None);

            await started.Task;

            // The following blocks the test:
            //
            // var ex = Assert.ThrowsAsync<Exception>(async () => await dog.Stop(TestContext.CurrentContext.CancellationToken));
            // Assert.That(ex.Message, Is.EqualTo("Simulated"));

            try
            {
                await dog.Stop(TestContext.CurrentContext.CancellationToken);
                Assert.Fail("Should have thrown an exception");
            }
            catch (Exception ex)
            {
                Assert.That(ex.Message, Is.EqualTo("Simulated"));
            }
        }

        [Test]
        public async Task On_failure_triggers_stopping()
        {
            var startAttempts = 0;
            var stopCalled = false;
            var started = new TaskCompletionSource<bool>();
            var restarted = new TaskCompletionSource<bool>();

            var dog = new Watchdog("test process", token =>
            {
                if (startAttempts == 0)
                {
                    started.SetResult(true);
                }
                else if (stopCalled)
                {
                    restarted.SetResult(true);
                }

                startAttempts++;
                return Task.CompletedTask;
            }, token =>
            {
                stopCalled = true;
                return Task.CompletedTask;
            }, x => { }, () => { }, TimeSpan.FromSeconds(1), log);

            await dog.Start(() => { }, CancellationToken.None);

            await started.Task;

            await dog.OnFailure("Simulated");

            await restarted.Task;

            await dog.Stop(TestContext.CurrentContext.CancellationToken);
        }

        [Test]
        public async Task When_first_start_attempt_works_it_recovers_from_further_errors()
        {
            string lastFailure = null;
            var runAttempts = 0;
            var recoveredFromError = new TaskCompletionSource<bool>();

            var dog = new Watchdog("test process", token =>
            {
                runAttempts++;

                if (runAttempts == 1)
                {
                    return Task.CompletedTask;
                }

                if (runAttempts == 2)
                {
                    throw new Exception("Simulated");
                }

                if (runAttempts == 3)
                {
                    Assert.That(lastFailure, Is.EqualTo("Simulated"));
                    throw new Exception("Simulated");
                }

                recoveredFromError.SetResult(true);
                return Task.CompletedTask;
            }, token => Task.CompletedTask, x => lastFailure = x, () => lastFailure = null, TimeSpan.FromSeconds(1), log);

            await dog.Start(() => { }, CancellationToken.None);

            await recoveredFromError.Task;

            await dog.Stop(TestContext.CurrentContext.CancellationToken);

            //Make sure failure is cleared
            Assert.That(lastFailure, Is.Null);
            Assert.That(runAttempts, Is.EqualTo(4));
        }

        [Test]
        public async Task When_first_start_attempt_fails_onFailedOnStartup_is_called()
        {
            string lastFailure = null;
            var onStartupFailureCalled = new TaskCompletionSource<bool>();

            var dog = new Watchdog("test process", token => throw new Exception("Simulated"), token => Task.CompletedTask, x => lastFailure = x, () => lastFailure = null, TimeSpan.FromSeconds(1), log);

            await dog.Start(() => { onStartupFailureCalled.SetResult(true); }, CancellationToken.None);

            await onStartupFailureCalled.Task;

            Assert.That(lastFailure, Is.EqualTo("Simulated"));
        }
    }
}