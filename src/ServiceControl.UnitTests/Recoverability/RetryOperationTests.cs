namespace ServiceControl.UnitTests.Operations
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class RetryOperationTests
    {
        [Test]
        public void Wait_should_set_wait_state()
        {
            var summary = new RetryOperation("abc123", RetryType.FailureGroup);
            summary.Wait(DateTime.UtcNow, "FailureGroup1");
            Assert.AreEqual(RetryState.Waiting, summary.RetryState);
            Assert.AreEqual(0, summary.NumberOfMessagesForwarded);
            Assert.AreEqual(0, summary.NumberOfMessagesPrepared);
            Assert.AreEqual(0, summary.NumberOfMessagesSkipped);
            Assert.AreEqual(0, summary.TotalNumberOfMessages);
            Assert.AreEqual("FailureGroup1", summary.Originator);
        }

        [Test]
        public void Wait_should_notify_wait()
        {
            var notifier = new TestNotifier();
            var summary = new RetryOperation("abc123", RetryType.FailureGroup) {Notifier = notifier};
            summary.Wait(DateTime.UtcNow);

            Assert.True(notifier.WaitNotified);
            Assert.AreEqual(0.00, notifier.Progress.Percentage);
        }

        [Test]
        public void Fail_should_set_failed()
        {
            var summary = new RetryOperation("abc123", RetryType.FailureGroup);
            summary.Fail();
            Assert.IsTrue(summary.Failed);
        }

        [Test]
        public void Prepare_should_set_prepare_state()
        {
            var summary = new RetryOperation("abc123", RetryType.FailureGroup);
            summary.Prepare(1000);
            Assert.AreEqual(RetryState.Preparing, summary.RetryState);
            Assert.AreEqual(0, summary.NumberOfMessagesPrepared);
            Assert.AreEqual(1000, summary.TotalNumberOfMessages);
        }

        [Test]
        public void Prepare_should_notify_prepare()
        {
            var notifier = new TestNotifier();
            var summary = new RetryOperation("abc123", RetryType.FailureGroup) { Notifier = notifier };
            summary.Prepare(1000);

            Assert.True(notifier.PrepareNotified);
            Assert.AreEqual(0, notifier.NumberOfMessagesPrepared);
            Assert.AreEqual(1000, notifier.TotalNumberOfMessages);
            Assert.AreEqual(0, notifier.Progress.Percentage);
        }

        [Test]
        public void Prepared_batch_should_set_prepare_state()
        {
            var summary = new RetryOperation("abc123", RetryType.FailureGroup);
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
            var summary = new RetryOperation("abc123", RetryType.FailureGroup) { Notifier = notifier };
            summary.Prepare(1000);
            summary.PrepareBatch(1000);

            Assert.True(notifier.PrepareBatchNotified);
            Assert.AreEqual(1000, notifier.NumberOfMessagesPrepared);
            Assert.AreEqual(1000, notifier.TotalNumberOfMessages);
            Assert.AreEqual(1.0, notifier.Progress.Percentage);
        }

        [Test]
        public void Forwarding_should_set_forwarding_state()
        {
            var summary = new RetryOperation("abc123", RetryType.FailureGroup);
            summary.Prepare(1000);
            summary.PrepareBatch(1000);
            summary.Forwarding();

            Assert.AreEqual(RetryState.Forwarding, summary.RetryState);
            Assert.AreEqual(0, summary.NumberOfMessagesForwarded);
            Assert.AreEqual(1000, summary.TotalNumberOfMessages);
        }
        
        [Test]
        public void Forwarding_batch_should_notify_forwarding()
        {
            var notifier = new TestNotifier();
            var summary = new RetryOperation("abc123", RetryType.FailureGroup) { Notifier = notifier };
            summary.Prepare(1000);
            summary.PrepareBatch(1000);
            summary.Forwarding();

            Assert.True(notifier.ForwardingNotified);
            Assert.AreEqual(0, notifier.NumberOfMessagesForwarded);
            Assert.AreEqual(1000, notifier.TotalNumberOfMessages);
            Assert.AreEqual(0.0, notifier.Progress.Percentage);
        }

        [Test]
        public void Batch_forwarded_should_set_forwarding_state()
        {
            var summary = new RetryOperation("abc123", RetryType.FailureGroup);
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
            var summary = new RetryOperation("abc123", RetryType.FailureGroup) { Notifier = notifier };
            summary.Prepare(1000);
            summary.PrepareBatch(1000);
            summary.Forwarding();
            summary.BatchForwarded(500);

            Assert.True(notifier.BatchForwardedNotified);
            Assert.AreEqual(500, notifier.NumberOfMessagesForwarded);
            Assert.AreEqual(1000, notifier.TotalNumberOfMessages);
            Assert.AreEqual(0.5, notifier.Progress.Percentage);
        }

        [Test]
        public void Batch_forwarded_all_forwarded_should_set_completed_state()
        {
            var summary = new RetryOperation("abc123", RetryType.FailureGroup);
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
            var summary = new RetryOperation("abc123", RetryType.FailureGroup) { Notifier = notifier };
            summary.Wait(DateTime.UtcNow, "FailureGroup1");
            summary.Prepare(1000);
            summary.PrepareBatch(1000);
            summary.Forwarding();
            summary.BatchForwarded(1000);

            Assert.True(notifier.CompletedNotified);
            Assert.AreEqual(false, notifier.Failed);
            Assert.AreEqual(1.0, notifier.Progress.Percentage);
            Assert.AreEqual("FailureGroup1", notifier.Originator);
        }


        [Test]
        public void Skip_should_set_update_skipped_messages()
        {
            var summary = new RetryOperation("abc123", RetryType.FailureGroup);
            summary.Wait(DateTime.UtcNow);
            summary.Prepare(2000);
            summary.PrepareBatch(1000);
            summary.Skip(1000);

            Assert.AreEqual(RetryState.Preparing, summary.RetryState);
            Assert.AreEqual(1000, summary.NumberOfMessagesSkipped);
        }

        [Test]
        public void Skip_should_complete_when_all_skipped()
        {
            var summary = new RetryOperation("abc123", RetryType.FailureGroup);
            summary.Wait(DateTime.UtcNow);
            summary.Prepare(1000);
            summary.PrepareBatch(1000);
            summary.Skip(1000);

            Assert.AreEqual(RetryState.Completed, summary.RetryState);
            Assert.AreEqual(1000, summary.NumberOfMessagesSkipped);
        }

        [Test]
        public void Skip_and_forward_combination_should_complete_when_done()
        {
            var summary = new RetryOperation("abc123", RetryType.FailureGroup);
            summary.Wait(DateTime.UtcNow);
            summary.Prepare(2000);
            summary.PrepareBatch(1000);
            summary.Skip(1000);
            summary.Forwarding();
            summary.BatchForwarded(1000);
            
            Assert.AreEqual(RetryState.Completed, summary.RetryState);
            Assert.AreEqual(1000, summary.NumberOfMessagesForwarded);
            Assert.AreEqual(1000, summary.NumberOfMessagesSkipped);
        }
    }

    public class TestNotifier : IRetryOperationProgressNotifier
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
        public Progress Progress { get; set; }
        public bool Failed{ get; private set; }
        public string Originator { get; private set; }

        public void Wait(string requestId, RetryType retryType, Progress progress, DateTime startTime)
        {
            WaitNotified = true;
            Progress = progress;
        }

        public void Prepare(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress, bool isFailed, DateTime startTime)
        {
            PrepareNotified = true;
            NumberOfMessagesPrepared = progress.MessagesPrepared;
            TotalNumberOfMessages = totalNumberOfMessages;
            Progress = progress;
        }

        public void PrepareBatch(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress, bool isFailed, DateTime startTime)
        {
            PrepareBatchNotified = true;
            NumberOfMessagesPrepared = progress.MessagesPrepared;
            TotalNumberOfMessages = totalNumberOfMessages;
            Progress = progress;
        }

        public void Forwarding(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress, bool isFailed, DateTime startTime)
        {
            ForwardingNotified = true;
            NumberOfMessagesForwarded = progress.MessagesForwarded;
            TotalNumberOfMessages = totalNumberOfMessages;
            Progress = progress;
        }

        public void BatchForwarded(string requestId, RetryType retryType, int totalNumberOfMessages, Progress progress, bool isFailed, DateTime startTime)
        {
            BatchForwardedNotified = true;
            NumberOfMessagesForwarded = progress.MessagesForwarded;
            TotalNumberOfMessages = totalNumberOfMessages;
            Progress = progress;
        }

        public void Completed(string requestId, RetryType retryType, bool failed, Progress progress, DateTime startTime, DateTime completionTime, string originator, int numberOfMessagesProcessed, DateTime last, string classifier)
        {
            CompletedNotified = true;
            Failed = failed;
            Progress = progress;
            Originator = originator;
        }
    }
}