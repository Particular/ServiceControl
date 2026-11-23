#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System.Collections.Generic;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class MigrationVerificationTests
{
    static MigrationCheckpoint Checkpoint(
        string categoryId = "UnresolvedAndRetryIssuedFailedMessages",
        MigrationCategoryState state = MigrationCategoryState.Complete,
        long copiedCount = 0,
        long skippedCount = 0,
        long alreadyPresentCount = 0,
        IReadOnlyDictionary<MigrationSkipReason, long>? skipReasons = null) =>
        new(categoryId, state, null, copiedCount, skippedCount, null, skipReasons, null, null, null, null, alreadyPresentCount);

    [TestCase("UnresolvedAndRetryIssuedFailedMessages", 100L, 85L, 5L, 10L)]
    public void Copied_skipped_and_already_present_rows_together_account_for_the_source(string categoryId, long sourceCount, long copied, long skipped, long alreadyPresent)
    {
        var result = MigrationVerification.Evaluate(
            categoryId, selected: true, sourceCount: sourceCount,
            Checkpoint(categoryId, copiedCount: copied, skippedCount: skipped, alreadyPresentCount: alreadyPresent,
                skipReasons: new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.PastRetention] = skipped }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Unaccounted, Is.Zero);
            Assert.That(result.Verdict, Is.EqualTo(MigrationVerificationVerdict.Ok), "harmless skips and rows the target already held are not a fault");
            Assert.That(result.AlreadyPresentCount, Is.EqualTo(alreadyPresent));
            Assert.That(result.SkipReasons[MigrationSkipReason.PastRetention], Is.EqualTo(skipped));
        }
    }

    [Test]
    public void A_done_category_whose_source_holds_rows_the_copy_never_read_is_flagged()
    {
        // Ten in the source now and the copy read six, and RavenDB only shrinks once the copy starts.
        var result = MigrationVerification.Evaluate(
            "UnresolvedAndRetryIssuedFailedMessages", selected: true, sourceCount: 10,
            Checkpoint(copiedCount: 6, skippedCount: 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Unaccounted, Is.EqualTo(4));
            Assert.That(result.Verdict, Is.EqualTo(MigrationVerificationVerdict.RowsNotRead));
            Assert.That(result.IsFault, Is.False, "a Done category is not outstanding, so rows it never read are reported, not failed");
        }
    }

    [Test]
    public void Rows_RavenDB_removed_after_the_copy_read_them_read_as_OK()
    {
        // The copy read ten event log items and RavenDB's expiry has deleted three of them since.
        var result = MigrationVerification.Evaluate(
            "EventLog", selected: true, sourceCount: 7,
            Checkpoint("EventLog", state: MigrationCategoryState.Complete, copiedCount: 10, skippedCount: 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Unaccounted, Is.EqualTo(-3), "the command prints this as rows removed since the copy read them");
            Assert.That(result.Verdict, Is.EqualTo(MigrationVerificationVerdict.Ok));
        }
    }

    // A selected category looks like this before anything touches it. Reading it as not selected would let a migration nobody ran print dashes and exit 0.
    [TestCase("EventLog", true, 3L, MigrationVerificationVerdict.Incomplete, 3L, true)]
    public void A_selected_category_with_no_row_is_incomplete_with_every_source_row_unaccounted(string categoryId, bool selected, long sourceCount, MigrationVerificationVerdict verdict, long? unaccounted, bool isFault)
    {
        var result = MigrationVerification.Evaluate(categoryId, selected, sourceCount, checkpoint: null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Unaccounted, Is.EqualTo(unaccounted), "with no row, nothing accounted for any source row");
            Assert.That(result.Verdict, Is.EqualTo(verdict));
            Assert.That(result.IsFault, Is.EqualTo(isFault));
        }
    }

    [TestCase("GroupComments", true, 100L, 1L, 96L)]
    public void An_abandoned_category_reports_its_loss_without_failing_the_command(string categoryId, bool selected, long? sourceCount, long skipped, long? expectedUnaccounted)
    {
        var result = MigrationVerification.Evaluate(
            categoryId, selected, sourceCount,
            Checkpoint(categoryId, state: MigrationCategoryState.Abandoned, copiedCount: 3, skippedCount: skipped));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Verdict, Is.EqualTo(MigrationVerificationVerdict.Abandoned), "the rows it gave up are lost whatever is selected now, so verify still lists them");
            Assert.That(result.Unaccounted, Is.EqualTo(expectedUnaccounted), "the loss is still stated, it just is not a fault");
            Assert.That(result.IsFault, Is.False);
        }
    }

    [Test]
    public void A_category_the_source_would_not_count_is_reported_as_unverified_rather_than_OK()
    {
        // This source cannot read the archive, so nothing here can say whether the copier saw every row.
        var result = MigrationVerification.Evaluate(
            "ArchivedAndResolvedFailedMessages", selected: true, sourceCount: null,
            Checkpoint("ArchivedAndResolvedFailedMessages", state: MigrationCategoryState.Complete, copiedCount: 900));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Unaccounted, Is.Null, "a subtraction from a number nobody produced is not zero");
            Assert.That(result.Verdict, Is.EqualTo(MigrationVerificationVerdict.NotCounted));
            Assert.That(result.IsFault, Is.False, "a category this source cannot read is a gap in the report, not a fault in the copy");
        }
    }

    [Test]
    public void A_selected_category_the_source_cannot_read_is_incomplete_while_its_row_is_outstanding()
    {
        // A NotStarted optional row that the source cannot read.
        var result = MigrationVerification.Evaluate(
            "EventLog", selected: true, sourceCount: null,
            Checkpoint("EventLog", state: MigrationCategoryState.NotStarted));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Unaccounted, Is.Null);
            Assert.That(result.Verdict, Is.EqualTo(MigrationVerificationVerdict.Incomplete), "an uncounted category must not hide one the copy has not finished");
            Assert.That(result.IsFault, Is.True);
        }
    }

    // A Failed category can have counts that balance, and the guard still refuses it.
    // A category still copying leaves every row it has not copied unaccounted for.
    [TestCase(MigrationCategoryState.Halted, 10L, 0L)]
    [TestCase(MigrationCategoryState.InProgress, 0L, 10L)]
    public void A_category_that_is_not_Done_is_incomplete_whatever_its_counts(MigrationCategoryState state, long copied, long expectedUnaccounted)
    {
        var result = MigrationVerification.Evaluate(
            "EventLog", selected: true, sourceCount: 10,
            Checkpoint("EventLog", state: state, copiedCount: copied, skippedCount: 0));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Unaccounted, Is.EqualTo(expectedUnaccounted));
            Assert.That(result.Verdict, Is.EqualTo(MigrationVerificationVerdict.Incomplete));
        }
    }
}
