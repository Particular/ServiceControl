namespace ServiceControl.UnitTests.Operations
{
    using System;
    using NUnit.Framework;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class RetryGroupEtagHelperTests
    {
        [Test]
        public void Id_changed_should_change_etag()
        {
            var group = new GroupOperation() {Id = "old"};
            var data = new[] { group };

            var knownEtag = EtagHelper.CalculateEtag(data);

            group.Id = "new";
            var newEtag = EtagHelper.CalculateEtag(data);

            Assert.AreNotEqual(knownEtag, newEtag);
        }

        [Test]
        public void Count_changed_should_change_etag()
        {
            var group = new GroupOperation() {Count = 1 };
            var data = new[] { group };

            var knownEtag = EtagHelper.CalculateEtag(data);

            group.Count = 2;
            var newEtag = EtagHelper.CalculateEtag(data);

            Assert.AreNotEqual(knownEtag, newEtag);
        }

        [Test]
        public void RetryStatus_changed_should_change_etag()
        {
            var group = new GroupOperation() { OperationStatus = RetryState.Waiting.ToString()};
            var data = new[] { group };

            var knownEtag = EtagHelper.CalculateEtag(data);

            group.OperationStatus = RetryState.Preparing.ToString();
            var newEtag = EtagHelper.CalculateEtag(data);

            Assert.AreNotEqual(knownEtag, newEtag);
        }

        [Test]
        public void RetryProgress_changed_should_change_etag()
        {
            var group = new GroupOperation();
            var data = new[] { group };

            var knownEtag = EtagHelper.CalculateEtag(data);

            group.OperationProgress = 0.01;
            var newEtag = EtagHelper.CalculateEtag(data);

            Assert.AreNotEqual(knownEtag, newEtag);
        }

        [Test]
        public void RetryStartTime_changed_should_change_etag()
        {
            var group = new GroupOperation();
            var data = new[] { group };

            var knownEtag = EtagHelper.CalculateEtag(data);

            group.OperationStartTime = DateTime.UtcNow;
            var newEtag = EtagHelper.CalculateEtag(data);

            Assert.AreNotEqual(knownEtag, newEtag);
        }

        [Test]
        public void RetryCompletionTime_changed_should_change_etag()
        {
            var group = new GroupOperation();
            var data = new[] { group };

            var knownEtag = EtagHelper.CalculateEtag(data);

            group.OperationCompletionTime = DateTime.UtcNow;
            var newEtag = EtagHelper.CalculateEtag(data);

            Assert.AreNotEqual(knownEtag, newEtag);
        }

        [Test]
        public void NeedUserAcknowledgement_changed_should_change_etag()
        {
            var group = new GroupOperation();
            var data = new[] { group };

            var knownEtag = EtagHelper.CalculateEtag(data);

            group.NeedUserAcknowledgement = true;
            var newEtag = EtagHelper.CalculateEtag(data);

            Assert.AreNotEqual(knownEtag, newEtag);
        }

        [Test]
        public void Changing_item_count_should_change_etag()
        {
            var data = new GroupOperation[0];
            var knownEtag = EtagHelper.CalculateEtag(data);

            var group = new GroupOperation();
            data = new[] { group };

            var newEtag = EtagHelper.CalculateEtag(data);

            Assert.AreNotEqual(knownEtag, newEtag);
        }
    }
}