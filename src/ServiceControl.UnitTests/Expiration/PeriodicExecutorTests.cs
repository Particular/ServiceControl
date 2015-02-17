namespace ServiceControl.UnitTests.Expiration
{
    using System;
    using System.Threading;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.RavenDB.Expiration;

    [TestFixture]
    public class PeriodicExecutorTests
    {
        [Test]
        public void If_execution_takes_longer_than_period_it_triggers_next_execution_immediately_after_previous()
        {
            var counter = 0;
            var failure = false;
            var lastEndTime = DateTime.MinValue;
            var @event = new ManualResetEventSlim(false);
            var delay = TimeSpan.Zero;
            var executor = new PeriodicExecutor(() =>
            {
                delay = DateTime.Now - lastEndTime;
                if (lastEndTime != DateTime.MinValue && delay > TimeSpan.FromMilliseconds(100))
                {
                    @event.Set();
                    failure = true;
                    return;
                }
                counter++;
                Thread.Sleep(2000);
                lastEndTime = DateTime.Now;
                if (counter == 2)
                {
                    @event.Set();
                }
            }, TimeSpan.FromSeconds(1));
            executor.Start(true);
            @event.Wait();
            executor.Stop();
            Assert.IsFalse(failure, string.Format("Time between finishing previous execution and starting this longer than {0} ms", delay));
        }

        [Test]
        public void If_execution_throws_it_does_not_kill_the_executor()
        {
            var first = true;
            var success = false;
            var @event = new ManualResetEventSlim(false);
            var executor = new PeriodicExecutor(() =>
            {
                if (first)
                {
                    first = false;
                    throw new Exception();
                }
                success = true;
                @event.Set();
            }, TimeSpan.FromSeconds(1));
            executor.Start(true);
            @event.Wait();
            executor.Stop();
            Assert.IsTrue(success);
        }

        [Test]
        public void Can_shutdown_while_waiting()
        {
            var @event = new ManualResetEventSlim(false);
            var executor = new PeriodicExecutor(@event.Set, TimeSpan.FromSeconds(10000));
            executor.Start(false);
            @event.Wait();
            Thread.Sleep(1000);
            executor.Stop();
            Assert.Pass();
        }

        [Test]
        public void Can_shutdown_when_not_started()
        {
            var executor = new PeriodicExecutor(() => {}, TimeSpan.FromSeconds(10000));
            executor.Stop();
            Assert.Pass();
        }
    }
}