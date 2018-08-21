namespace ServiceControl.UnitTests.Operations
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class RetryOperationTests
    {
        [Test]
        public async Task Wait_should_set_wait_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents());
            await summary.Wait(DateTime.UtcNow, "FailureGroup1");
            Assert.AreEqual(RetryState.Waiting, summary.RetryState);
            Assert.AreEqual(0, summary.NumberOfMessagesForwarded);
            Assert.AreEqual(0, summary.NumberOfMessagesPrepared);
            Assert.AreEqual(0, summary.NumberOfMessagesSkipped);
            Assert.AreEqual(0, summary.TotalNumberOfMessages);
            Assert.AreEqual("FailureGroup1", summary.Originator);
        }

        [Test]
        public void Fail_should_set_failed()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents());
            summary.Fail();
            Assert.IsTrue(summary.Failed);
        }

        [Test]
        public async Task Prepare_should_set_prepare_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents());
            await summary.Prepare(1000);
            Assert.AreEqual(RetryState.Preparing, summary.RetryState);
            Assert.AreEqual(0, summary.NumberOfMessagesPrepared);
            Assert.AreEqual(1000, summary.TotalNumberOfMessages);
        }

        [Test]
        public async Task Prepared_batch_should_set_prepare_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents());
            await summary.Prepare(1000);
            await summary.PrepareBatch(1000);
            Assert.AreEqual(RetryState.Preparing, summary.RetryState);
            Assert.AreEqual(1000, summary.NumberOfMessagesPrepared);
            Assert.AreEqual(1000, summary.TotalNumberOfMessages);
        }

        [Test]
        public async Task Forwarding_should_set_forwarding_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents());
            await summary.Prepare(1000);
            await summary.PrepareBatch(1000);
            await summary.Forwarding();

            Assert.AreEqual(RetryState.Forwarding, summary.RetryState);
            Assert.AreEqual(0, summary.NumberOfMessagesForwarded);
            Assert.AreEqual(1000, summary.TotalNumberOfMessages);
        }

        [Test]
        public async Task Batch_forwarded_should_set_forwarding_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents());
            await summary.Prepare(1000);
            await summary.PrepareBatch(1000);
            await summary.Forwarding();
            await summary.BatchForwarded(500);

            Assert.AreEqual(RetryState.Forwarding, summary.RetryState);
            Assert.AreEqual(500, summary.NumberOfMessagesForwarded);
            Assert.AreEqual(1000, summary.TotalNumberOfMessages);
        }

        [Test]
        public async Task Should_raise_domain_events()
        {
            var domainEvents = new FakeDomainEvents();
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, domainEvents);
            await summary.Prepare(1000);
            await summary.PrepareBatch(1000);
            await summary.Forwarding();
            await summary.BatchForwarded(1000);

            Assert.IsTrue(domainEvents.RaisedEvents[0] is RetryOperationPreparing);
            Assert.IsTrue(domainEvents.RaisedEvents[1] is RetryOperationPreparing);
            Assert.IsTrue(domainEvents.RaisedEvents[2] is RetryOperationForwarding);
            Assert.IsTrue(domainEvents.RaisedEvents[3] is RetryMessagesForwarded);
            Assert.IsTrue(domainEvents.RaisedEvents[4] is RetryOperationCompleted);
        }

        [Test]
        public async Task Batch_forwarded_all_forwarded_should_set_completed_state()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents());
            await summary.Prepare(1000);
            await summary.PrepareBatch(1000);
            await summary.Forwarding();
            await summary.BatchForwarded(1000);

            Assert.AreEqual(RetryState.Completed, summary.RetryState);
            Assert.AreEqual(1000, summary.NumberOfMessagesForwarded);
            Assert.AreEqual(1000, summary.TotalNumberOfMessages);
        }

        [Test]
        public async Task Skip_should_set_update_skipped_messages()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents());
            await summary.Wait(DateTime.UtcNow);
            await summary.Prepare(2000);
            await summary.PrepareBatch(1000);
            await summary.Skip(1000);

            Assert.AreEqual(RetryState.Preparing, summary.RetryState);
            Assert.AreEqual(1000, summary.NumberOfMessagesSkipped);
        }

        [Test]
        public async Task Skip_should_complete_when_all_skipped()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents());
            await summary.Wait(DateTime.UtcNow);
            await summary.Prepare(1000);
            await summary.PrepareBatch(1000);
            await summary.Skip(1000);

            Assert.AreEqual(RetryState.Completed, summary.RetryState);
            Assert.AreEqual(1000, summary.NumberOfMessagesSkipped);
        }

        [Test]
        public async Task Skip_and_forward_combination_should_complete_when_done()
        {
            var summary = new InMemoryRetry("abc123", RetryType.FailureGroup, new FakeDomainEvents());
            await summary.Wait(DateTime.UtcNow);
            await summary.Prepare(2000);
            await summary.PrepareBatch(1000);
            await summary.Skip(1000);
            await summary.Forwarding();
            await summary.BatchForwarded(1000);

            Assert.AreEqual(RetryState.Completed, summary.RetryState);
            Assert.AreEqual(1000, summary.NumberOfMessagesForwarded);
            Assert.AreEqual(1000, summary.NumberOfMessagesSkipped);
        }
    }
}