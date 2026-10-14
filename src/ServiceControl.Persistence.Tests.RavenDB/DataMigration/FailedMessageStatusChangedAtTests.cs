namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration
{
    using System;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence.RavenDB.DataMigration;

    [TestFixture]
    class FailedMessageStatusChangedAtTests
    {
        static readonly TimeSpan ErrorRetentionPeriod = TimeSpan.FromDays(10);

        [TestCase(FailedMessageStatus.Resolved)]
        [TestCase(FailedMessageStatus.Archived)]
        public void Reconstructs_the_status_change_instant_from_the_expiry_stamp(FailedMessageStatus status)
        {
            var statusChangedAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

            var resolved = FailedMessageStream.StatusChangedAt(new IngestedFailure().ToFailedMessage(status), statusChangedAt + ErrorRetentionPeriod, ErrorRetentionPeriod);

            Assert.That(resolved, Is.EqualTo(statusChangedAt));
        }

        [TestCase(FailedMessageStatus.Unresolved)]
        [TestCase(FailedMessageStatus.RetryIssued)]
        public void Records_the_newest_attempt_for_a_status_that_never_expires(FailedMessageStatus status)
        {
            var failure = new IngestedFailure();
            var stamp = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

            var resolved = FailedMessageStream.StatusChangedAt(failure.ToFailedMessage(status), stamp, ErrorRetentionPeriod);

            Assert.That(resolved, Is.EqualTo(failure.AttemptedAt), "a stamp on a status that never expires is not its status change");
        }

        [Test]
        public void Falls_back_to_the_newest_attempt_for_an_archived_message_with_no_stamp()
        {
            var failure = new IngestedFailure();

            var resolved = FailedMessageStream.StatusChangedAt(failure.ToFailedMessage(FailedMessageStatus.Archived), null, ErrorRetentionPeriod);

            Assert.That(resolved, Is.EqualTo(failure.AttemptedAt));
        }

        [Test]
        public void Returns_null_for_a_document_with_no_attempts_and_no_stamp()
        {
            var document = new IngestedFailure().ToFailedMessage();
            document.ProcessingAttempts.Clear();

            Assert.That(FailedMessageStream.StatusChangedAt(document, null, ErrorRetentionPeriod), Is.Null);
        }

        [Test]
        public void Returns_null_for_a_document_whose_attempt_list_is_null_and_has_no_stamp()
        {
            var document = new IngestedFailure().ToFailedMessage();
            document.ProcessingAttempts = null;

            Assert.That(FailedMessageStream.StatusChangedAt(document, null, ErrorRetentionPeriod), Is.Null, "the mapper skips a message with no attempts, so this must not throw and halt the category");
        }

        [Test]
        public void Reconstructs_an_instant_already_past_the_retention_cutoff_faithfully()
        {
            var expires = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var resolved = FailedMessageStream.StatusChangedAt(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Archived), expires, ErrorRetentionPeriod);

            Assert.That(resolved, Is.EqualTo(expires - ErrorRetentionPeriod), "the value the source implies, not one adjusted to survive the sweep");
        }

        [TestCase(DateTimeKind.Unspecified)]
        [TestCase(DateTimeKind.Utc)]
        public void Returns_a_UTC_instant_whatever_kind_the_attempt_was_read_with(DateTimeKind kind)
        {
            var document = new IngestedFailure().ToFailedMessage();
            var attemptedAt = new DateTime(2026, 9, 1, 8, 0, 0, kind);
            document.ProcessingAttempts[^1].AttemptedAt = attemptedAt;

            var resolved = FailedMessageStream.StatusChangedAt(document, null, ErrorRetentionPeriod);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(resolved.Value.Kind, Is.EqualTo(DateTimeKind.Utc), "MigrationMetadataKeys promises a UTC DateTime and DateTime equality ignores Kind");
                Assert.That(resolved.Value.Ticks, Is.EqualTo(attemptedAt.Ticks));
            }
        }

        [Test]
        public void Reads_an_expiry_stamp_with_no_kind_as_UTC()
        {
            var statusChangedAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Unspecified);

            var resolved = FailedMessageStream.StatusChangedAt(new IngestedFailure().ToFailedMessage(FailedMessageStatus.Archived), statusChangedAt + ErrorRetentionPeriod, ErrorRetentionPeriod);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(resolved.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
                Assert.That(resolved.Value.Ticks, Is.EqualTo(statusChangedAt.Ticks), "an unspecified instant is already UTC and must not be shifted by the local offset");
            }
        }

        [Test]
        public void Refuses_a_negative_source_retention_period()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                FailedMessageStream.StatusChangedAt(new IngestedFailure().ToFailedMessage(), null, TimeSpan.FromDays(-1)));

            Assert.That(ex.Message, Does.Contain("ErrorRetentionPeriod"));
        }
    }
}
