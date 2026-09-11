namespace ServiceControl.UnitTests.Operations
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Time.Testing;
    using NUnit.Framework;
    using ServiceControl.Persistence;
    using ServiceControl.Recoverability;
    using ServiceControl.UnitTests.Recoverability;

    [TestFixture]
    public class RetryOperationTests
    {
        readonly FakeTimeProvider fakeTime = new(DateTimeOffset.UtcNow);

        [Test]
        public async Task Wait_should_set_wait_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), TestRetryMetrics.Create(fakeTime), NullLogger.Instance, fakeTime);
            await summary.Wait(DateTime.UtcNow, "FailureGroup1");
            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Waiting));
                Assert.That(summary.NumberOfMessagesForwarded, Is.EqualTo(0));
                Assert.That(summary.NumberOfMessagesPrepared, Is.EqualTo(0));
                Assert.That(summary.NumberOfMessagesSkipped, Is.EqualTo(0));
                Assert.That(summary.TotalNumberOfMessages, Is.EqualTo(0));
                Assert.That(summary.Originator, Is.EqualTo("FailureGroup1"));
            }
        }

        [Test]
        public void Fail_should_set_failed()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), TestRetryMetrics.Create(fakeTime), NullLogger.Instance, fakeTime);
            summary.Fail();
            Assert.That(summary.Failed, Is.True);
        }

        [Test]
        public async Task Prepare_should_set_prepare_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), TestRetryMetrics.Create(fakeTime), NullLogger.Instance, fakeTime);
            await summary.Prepare(1000, StartedAt, null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Preparing));
                Assert.That(summary.NumberOfMessagesPrepared, Is.EqualTo(0));
                Assert.That(summary.TotalNumberOfMessages, Is.EqualTo(1000));
            }
        }

        [Test]
        public async Task Prepared_batch_should_set_prepare_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), TestRetryMetrics.Create(fakeTime), NullLogger.Instance, fakeTime);
            await summary.Prepare(1000, StartedAt, null);
            await summary.PrepareBatch(1000);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Preparing));
                Assert.That(summary.NumberOfMessagesPrepared, Is.EqualTo(1000));
                Assert.That(summary.TotalNumberOfMessages, Is.EqualTo(1000));
            }
        }

        [Test]
        public async Task Forwarding_should_set_forwarding_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), TestRetryMetrics.Create(fakeTime), NullLogger.Instance, fakeTime);
            await summary.Prepare(1000, StartedAt, null);
            await summary.PrepareBatch(1000);
            await summary.Forwarding();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Forwarding));
                Assert.That(summary.NumberOfMessagesForwarded, Is.EqualTo(0));
                Assert.That(summary.TotalNumberOfMessages, Is.EqualTo(1000));
            }
        }

        [Test]
        public async Task Batch_forwarded_should_set_forwarding_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), TestRetryMetrics.Create(fakeTime), NullLogger.Instance, fakeTime);
            await summary.Prepare(1000, StartedAt, null);
            await summary.PrepareBatch(1000);
            await summary.Forwarding();
            await summary.BatchForwarded(500);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Forwarding));
                Assert.That(summary.NumberOfMessagesForwarded, Is.EqualTo(500));
                Assert.That(summary.TotalNumberOfMessages, Is.EqualTo(1000));
            }
        }

        [Test]
        public async Task Should_raise_domain_events()
        {
            var domainEvents = new FakeDomainEvents();
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, domainEvents, TestRetryMetrics.Create(fakeTime), NullLogger.Instance, fakeTime);
            await summary.Prepare(1000, StartedAt, null);
            await summary.PrepareBatch(1000);
            await summary.Forwarding();
            await summary.BatchForwarded(1000);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(domainEvents.RaisedEvents[0] is RetryOperationPreparing, Is.True);
                Assert.That(domainEvents.RaisedEvents[1] is RetryOperationPreparing, Is.True);
                Assert.That(domainEvents.RaisedEvents[2] is RetryOperationForwarding, Is.True);
                Assert.That(domainEvents.RaisedEvents[3] is RetryMessagesForwarded, Is.True);
                Assert.That(domainEvents.RaisedEvents[4] is RetryOperationCompleted, Is.True);
            }
        }

        [Test]
        public async Task Batch_forwarded_all_forwarded_should_set_completed_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), TestRetryMetrics.Create(fakeTime), NullLogger.Instance, fakeTime);
            await summary.Prepare(1000, StartedAt, null);
            await summary.PrepareBatch(1000);
            await summary.Forwarding();
            await summary.BatchForwarded(1000);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Completed));
                Assert.That(summary.NumberOfMessagesForwarded, Is.EqualTo(1000));
                Assert.That(summary.TotalNumberOfMessages, Is.EqualTo(1000));
            }
        }

        [Test]
        public async Task Skip_should_set_update_skipped_messages()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), TestRetryMetrics.Create(fakeTime), NullLogger.Instance, fakeTime);
            await summary.Wait(DateTime.UtcNow);
            await summary.Prepare(2000, StartedAt, null);
            await summary.PrepareBatch(1000);
            await summary.Skip(1000);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Preparing));
                Assert.That(summary.NumberOfMessagesSkipped, Is.EqualTo(1000));
            }
        }

        [Test]
        public async Task Skip_should_complete_when_all_skipped()
        {
            var waitedAt = fakeTime.GetUtcNow().UtcDateTime;
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), TestRetryMetrics.Create(fakeTime), NullLogger.Instance, fakeTime);
            await summary.Wait(waitedAt);
            await summary.Prepare(1000, StartedAt, null);
            await summary.PrepareBatch(1000);
            await summary.Skip(1000);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Completed));
                Assert.That(summary.NumberOfMessagesSkipped, Is.EqualTo(1000));
                Assert.That(summary.Started, Is.EqualTo(waitedAt), "Prepare overwriting this would complete the operation before it started");
            }
        }

        [Test]
        public async Task Skip_and_forward_combination_should_complete_when_done()
        {
            var waitedAt = fakeTime.GetUtcNow().UtcDateTime;
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), TestRetryMetrics.Create(fakeTime), NullLogger.Instance, fakeTime);
            await summary.Wait(waitedAt);
            await summary.Prepare(2000, StartedAt, null);
            await summary.PrepareBatch(1000);
            await summary.Skip(1000);
            await summary.Forwarding();
            await summary.BatchForwarded(1000);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Completed));
                Assert.That(summary.NumberOfMessagesForwarded, Is.EqualTo(1000));
                Assert.That(summary.NumberOfMessagesSkipped, Is.EqualTo(1000));
                Assert.That(summary.Started, Is.EqualTo(waitedAt), "Prepare overwriting this would complete the operation before it started");
            }
        }

        [Test]
        public async Task A_second_run_does_not_inherit_the_first_runs_skipped_messages()
        {
            var summary = new InMemoryRetry("abc123", RetryType.All, new FakeDomainEvents(), TestRetryMetrics.Create(fakeTime), NullLogger.Instance, fakeTime);
            await summary.Prepare(2, StartedAt, "all messages");
            await summary.PrepareBatch(2);
            await summary.Forwarding();
            await summary.Skip(1);
            await summary.BatchForwarded(1);
            Assert.That(summary.RetryState, Is.EqualTo(RetryState.Completed), "the first run should have completed");

            await summary.Prepare(2, StartedAt.AddHours(1), "all messages");
            await summary.PrepareBatch(2);
            await summary.Forwarding();
            await summary.BatchForwarded(2);

            Assert.That(summary.RetryState, Is.EqualTo(RetryState.Completed), "a skip count left over from the previous run makes the operation miss its own finish line and sit on Forwarding for ever");
        }

        [Test]
        public async Task A_second_run_does_not_inherit_the_first_runs_failure()
        {
            var summary = new InMemoryRetry("abc123", RetryType.All, new FakeDomainEvents(), TestRetryMetrics.Create(fakeTime), NullLogger.Instance, fakeTime);
            await summary.Prepare(1, StartedAt, "all messages");
            summary.Fail();
            await summary.BatchForwarded(1);

            await summary.Prepare(1, StartedAt.AddHours(1), "all messages");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(summary.Failed, Is.False, "a run that has not failed yet would be written to retry history as a failure");
                Assert.That(summary.CompletionTime, Is.Null, "the previous run's completion time makes a running operation look finished");
            }
        }

        static readonly DateTime StartedAt = new(2026, 9, 2, 11, 0, 0, DateTimeKind.Utc);
    }
}