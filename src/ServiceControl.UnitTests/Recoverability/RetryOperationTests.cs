namespace ServiceControl.UnitTests.Operations
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;
    using ServiceControl.Persistence;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class RetryOperationTests
    {
        [Test]
        public async Task Wait_should_set_wait_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), NullLogger.Instance);
            await summary.Wait(DateTime.UtcNow, "FailureGroup1");
            Assert.Multiple(() =>
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Waiting));
                Assert.That(summary.NumberOfMessagesForwarded, Is.EqualTo(0));
                Assert.That(summary.NumberOfMessagesPrepared, Is.EqualTo(0));
                Assert.That(summary.NumberOfMessagesSkipped, Is.EqualTo(0));
                Assert.That(summary.TotalNumberOfMessages, Is.EqualTo(0));
                Assert.That(summary.Originator, Is.EqualTo("FailureGroup1"));
            });
        }

        [Test]
        public void Fail_should_set_failed()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), NullLogger.Instance);
            summary.Fail();
            Assert.That(summary.Failed, Is.True);
        }

        [Test]
        public async Task Prepare_should_set_prepare_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), NullLogger.Instance);
            await summary.Prepare(1000);
            Assert.Multiple(() =>
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Preparing));
                Assert.That(summary.NumberOfMessagesPrepared, Is.EqualTo(0));
                Assert.That(summary.TotalNumberOfMessages, Is.EqualTo(1000));
            });
        }

        [Test]
        public async Task Prepared_batch_should_set_prepare_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), NullLogger.Instance);
            await summary.Prepare(1000);
            await summary.PrepareBatch(1000);
            Assert.Multiple(() =>
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Preparing));
                Assert.That(summary.NumberOfMessagesPrepared, Is.EqualTo(1000));
                Assert.That(summary.TotalNumberOfMessages, Is.EqualTo(1000));
            });
        }

        [Test]
        public async Task Forwarding_should_set_forwarding_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), NullLogger.Instance);
            await summary.Prepare(1000);
            await summary.PrepareBatch(1000);
            await summary.Forwarding();

            Assert.Multiple(() =>
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Forwarding));
                Assert.That(summary.NumberOfMessagesForwarded, Is.EqualTo(0));
                Assert.That(summary.TotalNumberOfMessages, Is.EqualTo(1000));
            });
        }

        [Test]
        public async Task Batch_forwarded_should_set_forwarding_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), NullLogger.Instance);
            await summary.Prepare(1000);
            await summary.PrepareBatch(1000);
            await summary.Forwarding();
            await summary.BatchForwarded(500);

            Assert.Multiple(() =>
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Forwarding));
                Assert.That(summary.NumberOfMessagesForwarded, Is.EqualTo(500));
                Assert.That(summary.TotalNumberOfMessages, Is.EqualTo(1000));
            });
        }

        [Test]
        public async Task Should_raise_domain_events()
        {
            var domainEvents = new FakeDomainEvents();
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, domainEvents, NullLogger.Instance);
            await summary.Prepare(1000);
            await summary.PrepareBatch(1000);
            await summary.Forwarding();
            await summary.BatchForwarded(1000);

            Assert.Multiple(() =>
            {
                Assert.That(domainEvents.RaisedEvents[0] is RetryOperationPreparing, Is.True);
                Assert.That(domainEvents.RaisedEvents[1] is RetryOperationPreparing, Is.True);
                Assert.That(domainEvents.RaisedEvents[2] is RetryOperationForwarding, Is.True);
                Assert.That(domainEvents.RaisedEvents[3] is RetryMessagesForwarded, Is.True);
                Assert.That(domainEvents.RaisedEvents[4] is RetryOperationCompleted, Is.True);
            });
        }

        [Test]
        public async Task Batch_forwarded_all_forwarded_should_set_completed_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), NullLogger.Instance);
            await summary.Prepare(1000);
            await summary.PrepareBatch(1000);
            await summary.Forwarding();
            await summary.BatchForwarded(1000);

            Assert.Multiple(() =>
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Completed));
                Assert.That(summary.NumberOfMessagesForwarded, Is.EqualTo(1000));
                Assert.That(summary.TotalNumberOfMessages, Is.EqualTo(1000));
            });
        }

        [Test]
        public async Task Skip_should_set_update_skipped_messages()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), NullLogger.Instance);
            await summary.Wait(DateTime.UtcNow);
            await summary.Prepare(2000);
            await summary.PrepareBatch(1000);
            await summary.Skip(1000);

            Assert.Multiple(() =>
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Preparing));
                Assert.That(summary.NumberOfMessagesSkipped, Is.EqualTo(1000));
            });
        }

        [Test]
        public async Task Skip_should_complete_when_all_skipped()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), NullLogger.Instance);
            await summary.Wait(DateTime.UtcNow);
            await summary.Prepare(1000);
            await summary.PrepareBatch(1000);
            await summary.Skip(1000);

            Assert.Multiple(() =>
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Completed));
                Assert.That(summary.NumberOfMessagesSkipped, Is.EqualTo(1000));
            });
        }

        [Test]
        public async Task Skip_and_forward_combination_should_complete_when_done()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents(), NullLogger.Instance);
            await summary.Wait(DateTime.UtcNow);
            await summary.Prepare(2000);
            await summary.PrepareBatch(1000);
            await summary.Skip(1000);
            await summary.Forwarding();
            await summary.BatchForwarded(1000);

            Assert.Multiple(() =>
            {
                Assert.That(summary.RetryState, Is.EqualTo(RetryState.Completed));
                Assert.That(summary.NumberOfMessagesForwarded, Is.EqualTo(1000));
                Assert.That(summary.NumberOfMessagesSkipped, Is.EqualTo(1000));
            });
        }
    }
}