namespace ServiceControl.UnitTests.Operations
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using NUnit.Framework;
    using ServiceControl.Operations;

    [TestFixture]
    public class WatchdogTests
    {
        static ILog log = LogManager.GetLogger<WatchdogTests>();

        [Test]
        public async Task It_shuts_down_gracefully()
        {
            string lastFailure = null;
            var started = new TaskCompletionSource<bool>();
            var stopped = new TaskCompletionSource<bool>();

            var dog = new Watchdog(() =>
            {
                started.SetResult(true);
                return Task.CompletedTask;
            }, () =>
            {
                stopped.SetResult(true);
                return Task.CompletedTask;
            }, x => lastFailure = x, () => lastFailure = null, TimeSpan.FromSeconds(1), log, "test process");

            await dog.Start();

            await started.Task;

            await dog.Stop();

            await stopped.Task;
        }

        [Test]
        public async Task When_stop_fails_it_reports_the_failure()
        {
            string lastFailure = null;
            var started = new TaskCompletionSource<bool>();

            var dog = new Watchdog(
                () =>
                {
                    started.SetResult(true);
                    return Task.CompletedTask;
                }, 
                () => throw new Exception("Simulated"), 
                x => lastFailure = x, () => lastFailure = null, TimeSpan.FromSeconds(1), log, "test process");

            await dog.Start();

            await started.Task;

            await dog.Stop();

            Assert.AreEqual("Simulated", lastFailure);
        }

        [Test]
        public async Task On_failure_triggers_stopping()
        {
            string lastFailure = null;
            var startAttempts = 0;
            var stopCalled = false;
            var started = new TaskCompletionSource<bool>();
            var restarted = new TaskCompletionSource<bool>();

            var dog = new Watchdog(
                () =>
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
                },
                () =>
                {
                    stopCalled = true;
                    return Task.CompletedTask;
                }, 
                x => lastFailure = x, () => lastFailure = null, TimeSpan.FromSeconds(1), log, "test process");

            await dog.Start();

            await started.Task;

            await dog.OnFailure("Simulated");

            await restarted.Task;

            await dog.Stop();
        }

        [Test]
        public async Task When_start_fails_it_is_retried()
        {
            string lastFailure = null;
            var startAttempts = 0;
            var started = new TaskCompletionSource<bool>();

            var dog = new Watchdog(
                () =>
                {
                    if (startAttempts > 1)
                    {
                        //Make sure failures are reported
                        Assert.AreEqual("Simulated", lastFailure);
                    }
                    if (startAttempts < 5)
                    {
                        startAttempts++;
                        throw new Exception("Simulated");
                    }
                    started.SetResult(true);
                    return Task.CompletedTask;
                },
                () => Task.CompletedTask, 
                x => lastFailure = x, () => lastFailure = null, TimeSpan.FromSeconds(1), log, "test process");

            await dog.Start();

            await started.Task;

            await dog.Stop();

            //Make sure failure is cleared
            Assert.IsNull(lastFailure);
            Assert.AreEqual(5, startAttempts);
        }
    }
}