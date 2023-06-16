namespace ServiceControl.Audit.UnitTests.Infrastructure
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;

    [TestFixture]
    public class WatchdogTests
    {
        static ILog log = LogManager.GetLogger<WatchdogTests>();

        [Test]
        public async Task It_shuts_down_gracefully()
        {
            var started = new TaskCompletionSource<bool>();
            var stopped = new TaskCompletionSource<bool>();

            var dog = new Watchdog(token =>
            {
                started.SetResult(true);
                return Task.CompletedTask;
            }, token =>
            {
                stopped.SetResult(true);
                return Task.CompletedTask;
            }, x => { }, () => { }, TimeSpan.FromSeconds(1), log, "test process");

            await dog.Start(() => { });

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
                token =>
                {
                    started.SetResult(true);
                    return Task.CompletedTask;
                },
                token => throw new Exception("Simulated"),
                x => lastFailure = x, () => lastFailure = null, TimeSpan.FromSeconds(1), log, "test process");

            await dog.Start(() => { });

            await started.Task;

            await dog.Stop();

            Assert.AreEqual("Simulated", lastFailure);
        }

        [Test]
        public async Task On_failure_triggers_stopping()
        {
            var startAttempts = 0;
            var stopCalled = false;
            var started = new TaskCompletionSource<bool>();
            var restarted = new TaskCompletionSource<bool>();

            var dog = new Watchdog(
                token =>
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
                token =>
                {
                    stopCalled = true;
                    return Task.CompletedTask;
                },
                x => { }, () => { }, TimeSpan.FromSeconds(1), log, "test process");

            await dog.Start(() => { });

            await started.Task;

            await dog.OnFailure("Simulated");

            await restarted.Task;

            await dog.Stop();
        }

        [Test]
        public async Task When_start_works_it_recovers_from_errors()
        {
            string lastFailure = null;
            var runAttempts = 0;
            var recoveredFromError = new TaskCompletionSource<bool>();

            var dog = new Watchdog(
                token =>
                {
                    runAttempts++;
                    if (runAttempts > 1)
                    {
                        if (runAttempts < 4)
                        {
                            if (runAttempts > 2)
                            {
                                Assert.AreEqual("Simulated", lastFailure);
                            }
                            throw new Exception("Simulated");
                        }
                        else if (runAttempts == 4)
                        {
                            recoveredFromError.SetResult(true);
                        }
                    }
                    return Task.CompletedTask;
                },
                token => Task.CompletedTask,
                x => lastFailure = x, () => lastFailure = null, TimeSpan.FromSeconds(1), log, "test process");

            await dog.Start(() => { });

            await recoveredFromError.Task;

            await dog.Stop();

            //Make sure failure is cleared
            Assert.IsNull(lastFailure);
            Assert.AreEqual(4, runAttempts);
        }

        [Test]
        public async Task When_start_doesnt_work_onStartupFailure_is_called()
        {
            string lastFailure = null;
            var runAttempts = 0;
            var onStartupFailureCalled = new TaskCompletionSource<bool>();

            var dog = new Watchdog(
                token =>
                {
                    runAttempts++;
                    throw new Exception("Simulated");
                },
                token => Task.CompletedTask,
                x => lastFailure = x, () => lastFailure = null, TimeSpan.FromSeconds(1), log, "test process");

            await dog.Start(() => { onStartupFailureCalled.SetResult(true); });

            await onStartupFailureCalled.Task;

            Assert.AreEqual("Simulated", lastFailure);
            Assert.AreEqual(1, runAttempts);
        }
    }
}