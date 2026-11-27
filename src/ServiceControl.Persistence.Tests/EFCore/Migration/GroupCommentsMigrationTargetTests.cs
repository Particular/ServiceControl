namespace ServiceControl.Persistence.Tests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration.Writers;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.Infrastructure;

class GroupCommentsMigrationTargetTests : ErrorIngestionTestBase
{
    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    static readonly MigrationCategory GroupCommentsCategory = MigrationCategoryRegistry.Find(MigrationCategoryIds.GroupComments)!;

    [Test]
    public async Task A_comment_whose_group_id_is_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy()
    {
        var fits = new string('g', 64);
        var tooLong = new string('g', 65);
        var batch = BatchOf((fits, "Waiting on the payments team"), (tooLong, "Raised with the shipping team"));

        var details = await PreparedSkips.DetailsFrom(ServiceProvider, new GroupCommentsWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>()), batch);
        var result = await Target.Write(GroupCommentsCategory, batch, CheckpointAfter(tooLong));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1), "a group id exactly as long as its column fits");
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { $"GroupComment/{tooLong}" }));
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.ValueTooLong] = 1 }), "an id too long for its column would fail the whole insert and stop the category");
            Assert.That(await Target.Count(GroupCommentsCategory), Is.EqualTo(1), "--migration-verify reads this count");
            Assert.That(details.Single(), Does.EndWith($"takes at most 64 characters and the value holds 65, on document 'GroupComment/{tooLong}'"));
        }
    }

    [Test]
    public async Task A_comment_an_earlier_pass_copied_keeps_its_text_and_is_counted_as_already_present()
    {
        var failure = new IngestedFailure();
        var group = failure.Groups.Single();
        await Ingest(failure);

        var first = await Target.Write(GroupCommentsCategory, BatchOf((group.Id, "Waiting on the payments team")), CheckpointAfter(group.Id));
        var again = await Target.Write(GroupCommentsCategory, BatchOf((group.Id, "Edited in RavenDB since")), first.Saved);
        var view = (await GroupsStore.GetUnresolvedGroupsByClassifier(group.Type, null, new PagingInfo())).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(again.Copied, Is.Zero);
            Assert.That(again.AlreadyPresent, Is.EqualTo(1), "a retry re-reads the category from the start and finds what the first pass copied");
            Assert.That((view.Id, view.Comment), Is.EqualTo((group.Id, "Waiting on the payments team")), "the copy only inserts what is missing and never changes a row");
        }
    }

    [Test]
    public async Task A_comment_whose_group_id_the_key_treats_as_an_earlier_ones_is_reported_as_a_merge()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var batch = BatchOf(("group-1", "Waiting on the payments team"), ("group-1 ", "Raised with the shipping team"), ("group-1", "Edited in RavenDB since"));
        var keyIgnoresTrailingSpaces = dialect.KeyComparer(typeof(GroupCommentEntity), nameof(GroupCommentEntity.GroupId)).Equals("group-1", "group-1 ");

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, new GroupCommentsWriter(dialect), batch);
        var result = await Target.Write(GroupCommentsCategory, batch, CheckpointAfter("group-1"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Select(merge => merge.SourceId), Is.EqualTo(keyIgnoresTrailingSpaces ? new[] { "GroupComment/group-1 ", "GroupComment/group-1" } : ["GroupComment/group-1"]),
                "an id that differs only in trailing spaces is dropped only where the key column ignores them, an exact repeat on every provider, and the log has to say so");
            Assert.That(merges.Select(merge => merge.Detail), Has.All.Contains("kept 'group-1'"), "the target logs this line, so it has to name the group id it kept");
            Assert.That((result.Copied, result.AlreadyPresent), Is.EqualTo(keyIgnoresTrailingSpaces ? (1, 2) : (2, 1)),
                "on an empty target every row the insert drops is a merge, so the merges must account for every row counted as already present");
        }
    }

    [Test]
    public async Task A_comment_on_a_group_with_no_copied_message_is_reported_as_a_change()
    {
        var failure = new IngestedFailure();
        var group = failure.Groups.Single();
        await Ingest(failure);
        var longComment = new string('c', 150);
        var batch = BatchOf((group.Id, "Waiting on the payments team"), ("group-of-archived-messages", longComment));

        var changes = await PreparedSkips.ChangesFrom(ServiceProvider, new GroupCommentsWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>()), batch);
        var first = await Target.Write(GroupCommentsCategory, batch, CheckpointAfter("group-of-archived-messages"));
        var again = await Target.Write(GroupCommentsCategory, batch, first.Saved);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changes, Is.EqualTo(new[]
            {
                ("GroupComment/group-of-archived-messages",
                    "The group 'group-of-archived-messages' has no copied failed message. " +
                    "Its messages may be archived or resolved, gone from the source, skipped by the copy, or not reached before the unresolved failed messages copy was abandoned. " +
                    $"The comment was: '{longComment}'")
            }), "only the comment whose group has no copied message is reported");
            Assert.That((first.Copied, first.Changed), Is.EqualTo((2, 1)));
            Assert.That(again.Changed, Is.Zero, "a re-read finds the comment already present, and this write copied nothing");
        }
    }

    static MigrationBatch BatchOf(params (string GroupId, string Comment)[] comments) =>
        new([.. comments.Select(comment => new MigrationRow($"GroupComment/{comment.GroupId}", new GroupComment { Id = comment.GroupId, Comment = comment.Comment }, new Dictionary<string, object>()))], $"GroupComment/{comments[^1].GroupId}");

    static MigrationCheckpoint CheckpointAfter(string groupId) =>
        new(MigrationCategoryIds.GroupComments, MigrationCategoryState.InProgress, $"GroupComment/{groupId}", 0, 0, null, null, null, null, null, null);
}
