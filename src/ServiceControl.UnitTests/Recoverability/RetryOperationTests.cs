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
}