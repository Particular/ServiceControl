namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Hosting.Commands;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.Persistence.Tests;

[TestFixture]
[NonParallelizable]
class GroupCommentsMigrationTests : MigrationAcceptanceTest
{
    static readonly MigrationCategory GroupCommentsCategory = MigrationCategoryRegistry.Find(MigrationCategoryIds.GroupComments)!;

    [Test]
    public async Task Copies_each_comment_onto_its_group_and_an_orphaned_one_too_and_skips_a_null_or_blank_one_as_harmless()
    {
        var kept = new IngestedFailure();
        var cleared = new IngestedFailure();
        var neverWritten = new IngestedFailure();
        var spaces = new IngestedFailure();
        await SeedSourceWithBodies([.. new[] { kept, cleared, neverWritten, spaces }.Select(failure => (failure.ToFailedMessage(), failure.Body, failure.ContentType))]);
        await SeedSourceGroupComments(
            (kept.Groups.Single().Id, "Waiting on the payments team"),
            (cleared.Groups.Single().Id, ""),
            (neverWritten.Groups.Single().Id, null),
            (spaces.Groups.Single().Id, "   "),
            ("group-with-no-copied-message", "Raised with the shipping team"));
        await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);
        await CopyCategory(MigrationCategoryIds.UnresolvedFailedMessages);

        var result = await CopyCategory(MigrationCategoryIds.GroupComments);
        var comments = (await GroupsStore.GetUnresolvedGroupsByClassifier(kept.Groups.Single().Type, null, new PagingInfo())).ToDictionary(view => view.Id, view => view.Comment);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Saved.State, Is.EqualTo(MigrationCategoryState.Complete), "SQL never stores a blank comment, so leaving one behind loses nothing and must not keep ServiceControl closed");
            Assert.That(result.Copied, Is.EqualTo(2), "a comment whose group has no copied message is copied too, and left to the retention sweeper");
            Assert.That(await Target.Count(GroupCommentsCategory), Is.EqualTo(2), "--migration-verify reads this count");
            Assert.That(result.Saved.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BlankGroupComment] = 3 }), "a harmless skip is still counted, so status and verify can show it");
            Assert.That(comments[kept.Groups.Single().Id], Is.EqualTo("Waiting on the payments team"), "ServicePulse's group list shows the comment on the group it was written for");
            Assert.That(new[] { cleared, neverWritten, spaces }.Select(failure => comments[failure.Groups.Single().Id]), Is.All.Null, "the product shows no comment for a group whose comment was blank");
        }
    }

    [Test]
    public async Task A_comment_on_an_unresolved_group_survives_a_retention_sweep_between_the_offline_and_online_copies()
    {
        var unresolved = new IngestedFailure();
        var groupId = unresolved.Groups.Single().Id;
        await SeedSourceWithBodies((unresolved.ToFailedMessage(), unresolved.Body, unresolved.ContentType));
        await SeedSourceGroupComments((groupId, "Waiting on the payments team"));
        MigrationSettings();

        await MigrateCommand.Run(Settings, AllowingAnUnreleasedMigration());

        // The sweep that runs once ServiceControl opens, before the background copy has reached the unresolved message.
        await RunRetentionSweep();
        await RunHostUntilTheApiAnswers();
        await WaitUntil(
            async () => (await ReadCheckpoint(MigrationCategoryIds.GroupComments))?.State.IsFinished() == true,
            "the group comments copy finished",
            TimeSpan.FromMinutes(1));
        await RunRetentionSweep();

        var comments = (await GroupsStore.GetUnresolvedGroupsByClassifier(unresolved.Groups.Single().Type, null, new PagingInfo())).ToDictionary(view => view.Id, view => view.Comment);
        Assert.That(comments[groupId], Is.EqualTo("Waiting on the payments team"));
    }

    // The prefix is retyped because this project cannot reference the RavenDB persister that declares it.
    Task SeedSourceGroupComments(params (string GroupId, string Comment)[] comments) =>
        SeedSource([.. comments.Select(comment => ($"GroupComment/{comment.GroupId}", (object)new GroupComment { Id = $"GroupComment/{comment.GroupId}", Comment = comment.Comment }))]);
}
