namespace ServiceControl.UnitTests.Operations
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.WebApi;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class RetryGroupVersionTests
    {
        [Test]
        public void Id_changed_should_change_version()
        {
            var group = new GroupOperation { Id = "old" };
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.Id = "new";

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void Count_changed_should_change_version()
        {
            var group = new GroupOperation { Count = 1 };
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.Count = 2;

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void RetryStatus_changed_should_change_version()
        {
            var group = new GroupOperation { OperationStatus = RetryState.Waiting.ToString() };
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.OperationStatus = RetryState.Preparing.ToString();

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void RetryProgress_changed_should_change_version()
        {
            var group = new GroupOperation();
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.OperationProgress = 0.01;

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void RetryStartTime_changed_should_change_version()
        {
            var group = new GroupOperation();
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.OperationStartTime = DateTime.UtcNow;

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void RetryCompletionTime_changed_should_change_version()
        {
            var group = new GroupOperation();
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.OperationCompletionTime = DateTime.UtcNow;

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void NeedUserAcknowledgement_changed_should_change_version()
        {
            var group = new GroupOperation();
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.NeedUserAcknowledgement = true;

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void Comment_changed_should_change_version()
        {
            var group = new GroupOperation { Comment = "before" };
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.Comment = "after";

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void Title_changed_should_change_version()
        {
            var group = new GroupOperation { Title = "before" };
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.Title = "after";

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void Type_changed_should_change_version()
        {
            var group = new GroupOperation { Type = "before" };
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.Type = "after";

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void First_changed_should_change_version()
        {
            var group = new GroupOperation();
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.First = DateTime.UtcNow;

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void Last_changed_should_change_version()
        {
            var group = new GroupOperation();
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.Last = DateTime.UtcNow;

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void OperationFailed_changed_should_change_version()
        {
            var group = new GroupOperation { OperationFailed = false };
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.OperationFailed = true;

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void OperationMessagesCompletedCount_changed_should_change_version()
        {
            var group = new GroupOperation { OperationMessagesCompletedCount = 1 };
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.OperationMessagesCompletedCount = 2;

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void OperationRemainingCount_changed_should_change_version()
        {
            var group = new GroupOperation { OperationRemainingCount = 2 };
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.OperationRemainingCount = 1;

            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void A_message_completing_moves_the_version_even_when_the_rounded_progress_does_not()
        {
            // Progress is rounded to two decimals, so on a retry this big one message does not move it.
            var group = new GroupOperation
            {
                Id = "retry-1",
                OperationStatus = "Forwarding",
                OperationProgress = 0.2,
                OperationMessagesCompletedCount = 10_000,
                OperationRemainingCount = 40_000
            };
            var data = new[] { group };

            var knownVersion = VersionOf(data);

            group.OperationMessagesCompletedCount = 10_001;
            group.OperationRemainingCount = 39_999;

            Assert.That(group.OperationProgress, Is.EqualTo(0.2), "the premise: the rounded percentage has not moved");
            Assert.That(VersionOf(data).Matches(knownVersion), Is.False);
        }

        [Test]
        public void Changing_item_count_should_change_version()
        {
            var emptyVersion = VersionOf(Array.Empty<GroupOperation>());

            var oneGroup = new[] { new GroupOperation() };

            Assert.That(VersionOf(oneGroup).Matches(emptyVersion), Is.False,
                "an empty list is a representation like any other, so this compares two real versions rather than a version against nothing");
        }

        [Test]
        public void A_title_carrying_a_delimiter_cannot_impersonate_the_next_field()
        {
            var carrying = VersionOf([new GroupOperation { Title = "Shipping.Exception", Type = string.Empty }]);
            var split = VersionOf([new GroupOperation { Title = "Shipping", Type = "Exception" }]);

            Assert.That(carrying.Matches(split), Is.False,
                "two groups a client can tell apart must not share a validator");
        }

        [Test]
        public void Two_groups_cannot_digest_as_one_carrying_a_delimiter()
        {
            var two = VersionOf([new GroupOperation { Id = "a" }, new GroupOperation { Id = "b" }]);
            var oneForging = VersionOf([new GroupOperation { Id = "a|row1:1:b" }]);

            Assert.That(oneForging.Matches(two), Is.False,
                "a row able to pose as a longer row list would let user text pin the validator");
        }

        static DataVersion VersionOf(IReadOnlyList<GroupOperation> groups) =>
            groups.ToQueryStatsInfo("groups", groups.Count).Version;
    }
}
