namespace ServiceControl.UnitTests.Operations
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class RetryOperationSummaryTests
    {
        [Test]
        public void Wait_should_set_wait_state()
        {
            var summary = new RetryOperationSummary("abc123", RetryType.FailureGroup);
            summary.Wait();
            Assert.AreEqual(RetryState.Waiting, summary.RetryState);
            Assert.AreEqual(0, summary.NumberOfMessagesForwarded);
            Assert.AreEqual(0, summary.NumberOfMessagesPrepared);
            Assert.AreEqual(0, summary.TotalNumberOfMessages);
        }

        [Test]
        public void Wait_should_notify_wait()
        {
            var notifier = new TestNotifier();
            var summary = new RetryOperationSummary("abc123", RetryType.FailureGroup) {Notifier = notifier};
            summary.Wait();

            Assert.True(notifier.WaitNotified);
            Assert.AreEqual(0.05, notifier.Progression);
        }

        [Test]
        public void Fail_should_set_failed()
        {
            var summary = new RetryOperationSummary("abc123", RetryType.FailureGroup);
            summary.Fail();
            Assert.IsTrue(summary.Failed);
        }

        [Test]
        public void Prepare_should_set_prepare_state()
        {
            var summary = new RetryOperationSummary("abc123", RetryType.FailureGroup);
            summary.Prepare(1000);
            Assert.AreEqual(RetryState.Preparing, summary.RetryState);
            Assert.AreEqual(0, summary.NumberOfMessagesPrepared);
            Assert.AreEqual(1000, summary.TotalNumberOfMessages);
        }

        [Test]
        public void Prepare_should_notify_prepare()
        {
            var notifier = new TestNotifier();
            var summary = new RetryOperationSummary("abc123", RetryType.FailureGroup) { Notifier = notifier };
            summary.Prepare(1000);

            Assert.True(notifier.PrepareNotified);
            Assert.AreEqual(0, notifier.NumberOfMessagesPrepared);
            Assert.AreEqual(1000, notifier.TotalNumberOfMessages);
            Assert.AreEqual(0.05, notifier.Progression);
        }

        [Test]
        public void Prepared_batch_should_set_prepare_state()
        {
            var summary = new RetryOperationSummary("abc123", RetryType.FailureGroup);
            summary.Prepare(1000);
            summary.PrepareBatch(1000);
            Assert.AreEqual(RetryState.Preparing, summary.RetryState);
            Assert.AreEqual(1000, summary.NumberOfMessagesPrepared);
            Assert.AreEqual(1000, summary.TotalNumberOfMessages);
        }

        [Test]
        public void Prepared_batch_should_notify_prepared_batch()
        {
            var notifier = new TestNotifier();
            var summary = new RetryOperationSummary("abc123", RetryType.FailureGroup) { Notifier = notifier };
            summary.Prepare(1000);
            summary.PrepareBatch(1000);

            Assert.True(notifier.PrepareBatchNotified);
            Assert.AreEqual(1000, notifier.NumberOfMessagesPrepared);
            Assert.AreEqual(1000, notifier.TotalNumberOfMessages);
            Assert.AreEqual(0.525, notifier.Progression);
        }

        [Test]
        public void Forwarding_should_set_forwarding_state()
        {
            var summary = new RetryOperationSummary("abc123", RetryType.FailureGroup);
            summary.Prepare(1000);
            summary.PrepareBatch(1000);
            summary.Forwarding();

            Assert.AreEqual(RetryState.Forwarding, summary.RetryState);
            Assert.AreEqual(0, summary.NumberOfMessagesForwarded);
            Assert.AreEqual(1000, summary.TotalNumberOfMessages);
        }

        [Test]
        public void ForwardingAfterRestart_should_set_totalNumberOfMessages()
        {
            var summary = new RetryOperationSummary("abc123", RetryType.FailureGroup);
            summary.ForwardingAfterRestart(1000);

            Assert.AreEqual(RetryState.Forwarding, summary.RetryState);
            Assert.AreEqual(0, summary.NumberOfMessagesForwarded);
            Assert.AreEqual(1000, summary.TotalNumberOfMessages);
        }

        [Test]
        public void Forwarding_batch_should_notify_forwarding()
        {
            var notifier = new TestNotifier();
            var summary = new RetryOperationSummary("abc123", RetryType.FailureGroup) { Notifier = notifier };
            summary.Prepare(1000);
            summary.PrepareBatch(1000);
            summary.Forwarding();

            Assert.True(notifier.ForwardingNotified);
            Assert.AreEqual(0, notifier.NumberOfMessagesForwarded);
            Assert.AreEqual(1000, notifier.TotalNumberOfMessages);
            Assert.AreEqual(0.525, notifier.Progression);
        }

        [Test]
        public void Batch_forwarded_should_set_forwarding_state()
        {
            var summary = new RetryOperationSummary("abc123", RetryType.FailureGroup);
            summary.Prepare(1000);
            summary.PrepareBatch(1000);
            summary.Forwarding();
            summary.BatchForwarded(500);

            Assert.AreEqual(RetryState.Forwarding, summary.RetryState);
            Assert.AreEqual(500, summary.NumberOfMessagesForwarded);
            Assert.AreEqual(1000, summary.TotalNumberOfMessages);
        }

        [Test]
        public void Batch_forwarded_batch_should_notify_fatch_forwarded()
        {
            var notifier = new TestNotifier();
            var summary = new RetryOperationSummary("abc123", RetryType.FailureGroup) { Notifier = notifier };
            summary.Prepare(1000);
            summary.PrepareBatch(1000);
            summary.Forwarding();
            summary.BatchForwarded(500);

            Assert.True(notifier.BatchForwardedNotified);
            Assert.AreEqual(500, notifier.NumberOfMessagesForwarded);
            Assert.AreEqual(1000, notifier.TotalNumberOfMessages);
            Assert.AreEqual(0.7625, notifier.Progression);
        }

        [Test]
        public void Batch_forwarded_all_forwarded_should_set_completed_state()
        {
            var summary = new RetryOperationSummary("abc123", RetryType.FailureGroup);
            summary.Prepare(1000);
            summary.PrepareBatch(1000);
            summary.Forwarding();
            summary.BatchForwarded(1000);

            Assert.AreEqual(RetryState.Completed, summary.RetryState);
            Assert.AreEqual(1000, summary.NumberOfMessagesForwarded);
            Assert.AreEqual(1000, summary.TotalNumberOfMessages);
        }

        [Test]
        public void Batch_forwarded_all_forwarded_should_notify_completed()
        {
            var notifier = new TestNotifier();
            var summary = new RetryOperationSummary("abc123", RetryType.FailureGroup) { Notifier = notifier };
            summary.Prepare(1000);
            summary.PrepareBatch(1000);
            summary.Forwarding();
            summary.BatchForwarded(1000);

            Assert.True(notifier.CompletedNotified);
            Assert.AreEqual(false, notifier.Failed);
            Assert.AreEqual(1.0, notifier.Progression);
        }
    }

    public class TestNotifier : IRetryOperationProgressionNotifier
    {
        public bool WaitNotified { get; private set; }
        public bool PrepareNotified { get; private set; }
        public bool PrepareBatchNotified { get; private set; }
        public bool ForwardingNotified { get; private set; }
        public bool BatchForwardedNotified { get; private set; }
        public bool CompletedNotified { get; private set; }

        public int NumberOfMessagesPrepared  { get; private set; }
        public int NumberOfMessagesForwarded { get; private set; }
        public int TotalNumberOfMessages { get; private set; }
        public double Progression { get; set; }
        public bool Failed{ get; private set; }

        public void Wait(string requestId, RetryType retryType, double progression)
        {
            WaitNotified = true;
            Progression = progression;
        }

        public void Prepare(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages, double progression)
        {
            PrepareNotified = true;
            NumberOfMessagesPrepared = numberOfMessagesPrepared;
            TotalNumberOfMessages = totalNumberOfMessages;
            Progression = progression;
        }

        public void PrepareBatch(string requestId, RetryType retryType, int numberOfMessagesPrepared, int totalNumberOfMessages, double progression)
        {
            PrepareBatchNotified = true;
            NumberOfMessagesPrepared = numberOfMessagesPrepared;
            TotalNumberOfMessages = totalNumberOfMessages;
            Progression = progression;
        }

        public void Forwarding(string requestId, RetryType retryType, int numberOfMessagesForwarded, int totalNumberOfMessages, double progression)
        {
            ForwardingNotified = true;
            NumberOfMessagesForwarded = numberOfMessagesForwarded;
            TotalNumberOfMessages = totalNumberOfMessages;
            Progression = progression;
        }

        public void BatchForwarded(string requestId, RetryType retryType, int numberOfMessagesForwarded, int totalNumberOfMessages, double progression)
        {
            BatchForwardedNotified = true;
            NumberOfMessagesForwarded = numberOfMessagesForwarded;
            TotalNumberOfMessages = totalNumberOfMessages;
            Progression = progression;
        }

        public void Completed(string requestId, RetryType retryType, bool failed, double progression, DateTime completionTime)
        {
            CompletedNotified = true;
            Failed = failed;
            Progression = progression;
        }
    }
}