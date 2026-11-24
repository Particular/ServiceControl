namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Hosting.Commands;
using ServiceControl.MessageFailures;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.Tests;
using ServiceControl.Recoverability.ExternalIntegration;

/// <summary>
/// The required failed message category copies the errors of the hour before the newest unresolved message, in every status,
/// besides the retry-issued messages and the claim holders of any age, so the host opens with them in place. The source is
/// stopped while it is copied, so the newest unresolved message is the same on every read and nothing is persisted for it.
/// </summary>
[TestFixture]
[NonParallelizable]
class RecentErrorsMigrationTests : MigrationAcceptanceTest
{
    const string RequiredCategoryId = MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages;

    [Test]
    public async Task Copies_the_recent_errors_of_every_status_once_with_their_bodies_groups_and_claims()
    {
        var retryIssuedClaimed = await Seed(FailedMessageStatus.RetryIssued, claimed: true);
        var retryIssuedReleased = await Seed(FailedMessageStatus.RetryIssued);
        var archivedClaimed = await Seed(FailedMessageStatus.Archived, claimed: true);
        var resolvedClaimed = await Seed(FailedMessageStatus.Resolved, claimed: true);
        var archivedPlain = await Seed(FailedMessageStatus.Archived);
        var resolvedPlain = await Seed(FailedMessageStatus.Resolved);
        var unresolvedClaimed = await Seed(FailedMessageStatus.Unresolved, claimed: true);
        var newestUnresolved = await Seed(FailedMessageStatus.Unresolved);

        var copy = await CopyCategory(RequiredCategoryId);

        Seeded[] copiedIds = [retryIssuedClaimed, retryIssuedReleased, archivedClaimed, resolvedClaimed, archivedPlain, resolvedPlain, unresolvedClaimed, newestUnresolved];
        Seeded[] claimedIds = [retryIssuedClaimed, archivedClaimed, resolvedClaimed, unresolvedClaimed];
        var rows = await QueryTarget(db => db.FailedMessages.AsNoTracking().ToDictionaryAsync(row => row.UniqueMessageId));
        var claims = await QueryTarget(db => db.FailedMessageRetries.AsNoTracking().Select(claim => claim.UniqueMessageId).ToListAsync());
        var groups = await QueryTarget(db => db.FailedMessageGroups.AsNoTracking().Select(group => group.FailedMessageUniqueId).ToListAsync());
        var category = MigrationCategoryRegistry.Find(RequiredCategoryId)!;
        await using var source = await OpenSource();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.Copied, Is.EqualTo(copiedIds.Length), "a message that is retry-issued, holds a claim and is recent is still one row");
            Assert.That(rows.Keys, Is.EquivalentTo(copiedIds.Select(failure => failure.UniqueMessageId)));
            Assert.That(copiedIds.Select(failure => rows[failure.UniqueMessageId].Status), Is.EqualTo(copiedIds.Select(failure => failure.Status)));
            Assert.That(claims, Is.EquivalentTo(claimedIds.Select(failure => failure.UniqueMessageId)), "every claim the source held is copied as one claim and no other message gets one");
            Assert.That(groups, Is.EquivalentTo(copiedIds.Select(failure => failure.UniqueMessageId)), "every copied message keeps its failure group");
            Assert.That(await Target.Count(category), Is.EqualTo(copiedIds.Length));
            Assert.That(await source.Count(category), Is.EqualTo(copiedIds.Length), "the source counts the set the target holds");
        }

        foreach (var failure in copiedIds)
        {
            await AssertBody(failure);
        }

        var optional = await CopyCategory(MigrationCategoryIds.UnresolvedFailedMessages);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(optional.Copied, Is.Zero, "both unresolved messages are recent errors");
            Assert.That(optional.AlreadyPresent, Is.EqualTo(2));
            Assert.That(await QueryTarget(db => db.FailedMessages.CountAsync()), Is.EqualTo(copiedIds.Length));
            Assert.That(await QueryTarget(db => db.FailedMessageRetries.CountAsync()), Is.EqualTo(claimedIds.Length), "the optional copy adds no claim");
        }
    }

    [Test]
    public async Task Copies_only_the_retry_issued_messages_and_claim_holders_when_the_source_has_no_unresolved_message()
    {
        var retryIssued = await Seed(FailedMessageStatus.RetryIssued);
        var archivedClaimed = await Seed(FailedMessageStatus.Archived, claimed: true);
        var archivedPlain = await Seed(FailedMessageStatus.Archived);
        var resolvedPlain = await Seed(FailedMessageStatus.Resolved);

        var copy = await CopyCategory(RequiredCategoryId);

        var copied = await QueryTarget(db => db.FailedMessages.Select(row => row.UniqueMessageId).ToListAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.Copied, Is.EqualTo(2));
            Assert.That(copied, Is.EquivalentTo(new[] { retryIssued.UniqueMessageId, archivedClaimed.UniqueMessageId }), "with no unresolved message there is no anchor, so the recent hour is not copied");
            Assert.That(copied, Does.Not.Contain(archivedPlain.UniqueMessageId).And.Not.Contain(resolvedPlain.UniqueMessageId));
        }
    }

    [Test]
    public async Task A_retry_reset_and_a_second_copy_read_the_same_set_because_the_source_decides_it()
    {
        await Seed(FailedMessageStatus.Archived);
        await Seed(FailedMessageStatus.Unresolved);
        await Seed(FailedMessageStatus.Resolved);

        var first = await CopyCategory(RequiredCategoryId);
        await Target.ResetForRetry(RetryResetMigrationTests.ResetForRetry(await ReadCheckpoint(RequiredCategoryId)));
        var second = await CopyCategory(RequiredCategoryId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Copied, Is.EqualTo(3));
            Assert.That(second.AlreadyPresent, Is.EqualTo(3), "the second read selected the same three messages");
            Assert.That(second.Copied, Is.Zero);
        }
    }

    [Test]
    public async Task The_offline_command_copies_the_recent_message_before_the_host_opens()
    {
        await SeedSourceKnownEndpoints("Sales");
        var failure = await Seed(FailedMessageStatus.Unresolved);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        await MigrateCommand.Run(Settings, AllowingAnUnreleasedMigration(), cancellation.Token);

        var failedMessages = await ReadCheckpoint(RequiredCategoryId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(failedMessages.CopiedCount, Is.EqualTo(1), "the newest unresolved message is copied before the host opens");
            Assert.That(await FindFailedMessage(failure.UniqueMessageId), Is.Not.Null);
        }
    }

    [Test]
    public async Task A_pending_MessageFailed_event_for_a_recent_message_dispatches_with_the_message_already_copied()
    {
        var failure = await Seed(FailedMessageStatus.Unresolved);
        await SeedSource(($"ExternalIntegrationDispatchRequests/{Guid.NewGuid()}", new ExternalIntegrationDispatchRequest { DispatchContext = new MessageFailedPublisher.DispatchContext { FailedMessageId = failure.UniqueMessageId } }));

        await CopyCategory(RequiredCategoryId);
        await CopyCategory(MigrationCategoryIds.PendingIntegrationEvents);

        object[] dispatched = [];
        ExternalIntegrationStore.Subscribe((contexts, _) =>
        {
            dispatched = contexts;
            return Task.CompletedTask;
        });
        await ExternalIntegrationStore.DispatchNow();

        var publisher = new MessageFailedPublisher(FailedMessageQueryStore);
        var events = (await publisher.PublishEventsForOwnContexts(dispatched)).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(((MessageFailedPublisher.DispatchContext)dispatched.Single()).FailedMessageId, Is.EqualTo(failure.UniqueMessageId));
            Assert.That(events, Has.Length.EqualTo(1), "a request whose message is missing from the target dispatches nothing and is deleted");
        }

        await AssertBody(failure);
    }

    async Task AssertBody(Seeded failure)
    {
        var body = await BodyStorage.TryFetch(failure.UniqueMessageId.ToString());

        Assert.That(body.State, Is.EqualTo(MessageBodyState.Available), $"the body of {failure.Status} message {failure.UniqueMessageIdString}");
        Assert.That(body.Content.ContentType, Is.EqualTo(failure.ContentType));
        Assert.That(await ReadAll(body.Content.Stream), Is.EqualTo(failure.Body));
    }

    // Seeded with a body, a failure group and, when claimed, the retry claim RavenDB would hold.
    async Task<Seeded> Seed(FailedMessageStatus status, bool claimed = false)
    {
        var failure = new IngestedFailure();
        await SeedSourceWithBody(failure.ToFailedMessage(status), failure.Body, failure.ContentType);

        if (claimed)
        {
            await SeedSourceRetryClaim(failure.UniqueMessageIdString);
        }

        return new Seeded(failure.UniqueMessageId, failure.UniqueMessageIdString, status, failure.Body, failure.ContentType);
    }

    record Seeded(Guid UniqueMessageId, string UniqueMessageIdString, FailedMessageStatus Status, byte[] Body, string ContentType);

    static async Task<byte[]> ReadAll(Stream stream)
    {
        await using (stream)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return buffer.ToArray();
        }
    }
}
