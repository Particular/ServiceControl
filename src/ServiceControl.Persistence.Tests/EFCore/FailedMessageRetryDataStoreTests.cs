namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Entities;

class FailedMessageRetryDataStoreTests : ErrorIngestionTestBase
{
    static readonly DateTime Noon = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task RemoveFailedMessageRetry_deletes_the_claim_row()
    {
        var id = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon, queueAddress: "Shipping");
        await SeedRetryClaim(id);

        await FailedMessageRetryStore.RemoveFailedMessageRetry(id.ToString());

        Assert.That(await CountRetryRows(id), Is.EqualTo(0), "the retry claim row should be deleted");
    }

    [Test]
    public async Task RemoveFailedMessageRetry_is_idempotent_when_no_row_exists()
    {
        var id = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon);

        // No claim row was seeded; the call must not throw.
        Assert.DoesNotThrowAsync(() => FailedMessageRetryStore.RemoveFailedMessageRetry(id.ToString()));
        Assert.That(await CountRetryRows(id), Is.EqualTo(0));
    }

    [Test]
    public async Task RemoveFailedMessageRetry_ignores_a_non_guid_id()
    {
        // Consumers always pass a Guid string, but the store must not throw on garbage input.
        Assert.DoesNotThrowAsync(() => FailedMessageRetryStore.RemoveFailedMessageRetry("not-a-guid"));
    }

    [Test]
    public async Task RemoveFailedMessageRetry_does_not_touch_the_failed_message_row()
    {
        var id = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon, queueAddress: "Shipping");
        await SeedRetryClaim(id);

        await FailedMessageRetryStore.RemoveFailedMessageRetry(id.ToString());

        var failed = await GetFailedMessage(id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(failed.Status, Is.EqualTo(FailedMessageStatus.RetryIssued), "status must be unchanged");
            Assert.That(failed.LastModified, Is.EqualTo(Noon), "LastModified must be unchanged");
            Assert.That(failed.FailingEndpointAddress, Is.EqualTo("Shipping"));
        }
    }

    [Test]
    public async Task GetRetryPendingMessages_returns_only_RetryIssued_messages_in_the_window()
    {
        var inWindow = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon, queueAddress: "Shipping");
        var beforeWindow = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon.AddHours(-2));
        var afterWindow = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon.AddHours(2));
        var unresolvedInWindow = await SeedFailedMessage(FailedMessageStatus.Unresolved, Noon, queueAddress: "Shipping");
        var resolvedInWindow = await SeedFailedMessage(FailedMessageStatus.Resolved, Noon, queueAddress: "Shipping");
        var archivedInWindow = await SeedFailedMessage(FailedMessageStatus.Archived, Noon, queueAddress: "Shipping");

        var ids = await FailedMessageRetryStore.GetRetryPendingMessages(Noon.AddHours(-1), Noon.AddHours(1), "Shipping");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ids, Does.Contain(inWindow.ToString()).IgnoreCase, "the in-window RetryIssued message must be returned");
            Assert.That(ids, Does.Not.Contain(beforeWindow.ToString()).IgnoreCase, "messages before the window are excluded");
            Assert.That(ids, Does.Not.Contain(afterWindow.ToString()).IgnoreCase, "messages after the window are excluded");
            Assert.That(ids, Does.Not.Contain(unresolvedInWindow.ToString()).IgnoreCase, "Unresolved messages are excluded");
            Assert.That(ids, Does.Not.Contain(resolvedInWindow.ToString()).IgnoreCase, "Resolved messages are excluded");
            Assert.That(ids, Does.Not.Contain(archivedInWindow.ToString()).IgnoreCase, "Archived messages are excluded");
        }
    }

    [Test]
    public async Task GetRetryPendingMessages_returns_raw_guid_string_ids()
    {
        var id = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon, queueAddress: "Shipping");

        var ids = await FailedMessageRetryStore.GetRetryPendingMessages(Noon.AddHours(-1), Noon.AddHours(1), "Shipping");

        Assert.That(ids, Has.Length.EqualTo(1));
        Assert.That(ids[0], Is.EqualTo(id.ToString()).IgnoreCase, "consumers Guid.Parse the returned id, so it must be a raw Guid string");
        Assert.That(Guid.TryParse(ids[0], out _), Is.True);
    }

    [Test]
    public async Task GetRetryPendingMessages_filters_by_queue_address()
    {
        var shipping = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon, queueAddress: "Shipping");
        var billing = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon, queueAddress: "Billing");

        var ids = await FailedMessageRetryStore.GetRetryPendingMessages(Noon.AddHours(-1), Noon.AddHours(1), "Billing");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ids, Does.Contain(billing.ToString()).IgnoreCase);
            Assert.That(ids, Does.Not.Contain(shipping.ToString()).IgnoreCase);
        }
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public async Task GetRetryPendingMessages_with_a_null_or_blank_queue_address_does_not_match_other_queues(string queueAddress)
    {
        await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon, queueAddress: "Shipping");
        await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon, queueAddress: "Billing");

        var ids = await FailedMessageRetryStore.GetRetryPendingMessages(Noon.AddHours(-1), Noon.AddHours(1), queueAddress);

        Assert.That(ids, Is.Empty);
    }

    [Test]
    public async Task ProcessPendingRetries_invokes_callback_once_per_matching_message_with_raw_guid_string()
    {
        var first = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon, queueAddress: "Shipping");
        var second = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon, queueAddress: "Shipping");
        var outOfWindow = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon.AddHours(5), queueAddress: "Shipping");
        var unresolved = await SeedFailedMessage(FailedMessageStatus.Unresolved, Noon, queueAddress: "Shipping");

        var captured = new List<string>();
        var count = 0;

        await FailedMessageRetryStore.ProcessPendingRetries(
            Noon.AddHours(-1),
            Noon.AddHours(1),
            "Shipping",
            (id, _) =>
            {
                captured.Add(id);
                count++;
                return Task.CompletedTask;
            });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(count, Is.EqualTo(2), "only the two in-window RetryIssued messages match");
            Assert.That(captured, Does.Contain(first.ToString()).IgnoreCase);
            Assert.That(captured, Does.Contain(second.ToString()).IgnoreCase);
            Assert.That(captured, Does.Not.Contain(outOfWindow.ToString()).IgnoreCase);
            Assert.That(captured, Does.Not.Contain(unresolved.ToString()).IgnoreCase);
            Assert.That(captured, Has.All.Matches<string>(id => Guid.TryParse(id, out _)), "every callback id must be a raw Guid string");
        }
    }

    [Test]
    public async Task ProcessPendingRetries_with_null_queue_address_matches_all_queues()
    {
        var shipping = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon, queueAddress: "Shipping");
        var billing = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon, queueAddress: "Billing");

        var captured = new List<string>();

        await FailedMessageRetryStore.ProcessPendingRetries(
            Noon.AddHours(-1),
            Noon.AddHours(1),
            null,
            (id, _) =>
            {
                captured.Add(id);
                return Task.CompletedTask;
            });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(captured, Does.Contain(shipping.ToString()).IgnoreCase);
            Assert.That(captured, Does.Contain(billing.ToString()).IgnoreCase);
        }
    }

    [Test]
    public async Task GetFailedMessageBody_returns_inline_BodyText_bytes()
    {
        var id = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon, bodyText: "hello body");

        var body = await FailedMessageRetryStore.GetFailedMessageBody(id.ToString());

        Assert.That(body, Is.Not.Null, "an inline body must be returned");
        Assert.That(body, Is.EqualTo(Encoding.UTF8.GetBytes("hello body")));
    }

    [Test]
    public void GetFailedMessageBody_throws_for_a_nonexistent_message()
    {
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            FailedMessageRetryStore.GetFailedMessageBody(Guid.NewGuid().ToString()));
    }

    [Test]
    public async Task GetFailedMessageBody_throws_when_the_body_is_unavailable()
    {
        var id = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon);

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            FailedMessageRetryStore.GetFailedMessageBody(id.ToString()));
    }

    [Test]
    public async Task GetFailedMessageBody_returns_external_storage_body_when_BodyStoredExternally()
    {
        var id = await SeedFailedMessage(FailedMessageStatus.RetryIssued, Noon, bodyStoredExternally: true, bodyText: null);
        var expected = Encoding.UTF8.GetBytes("external body payload");

        await RecordedBodies.WriteBody(id.ToString(), expected, "text/plain");

        var body = await FailedMessageRetryStore.GetFailedMessageBody(id.ToString());

        Assert.That(body, Is.Not.Null);
        Assert.That(body, Is.EqualTo(expected));
    }

    [Test]
    public async Task RemoveFailedMessageRetry_removes_the_message_from_its_batch_staging_set()
    {
        var id = await SeedFailedMessage(FailedMessageStatus.Unresolved, Noon, queueAddress: "Shipping");
        var batchId = Guid.NewGuid();
        await SeedRetryClaim(id, batchId);

        // Before removal, the batch can stage the claimed message.
        var before = await RetryStagingStore.GetMessagesToStage(batchId.ToString());
        Assert.That(before.Any(m => string.Equals(m.UniqueMessageId, id.ToString(), StringComparison.OrdinalIgnoreCase)), Is.True, "the claimed message should be staged by its batch");

        await FailedMessageRetryStore.RemoveFailedMessageRetry(id.ToString());

        var after = await RetryStagingStore.GetMessagesToStage(batchId.ToString());
        Assert.That(after.Any(m => string.Equals(m.UniqueMessageId, id.ToString(), StringComparison.OrdinalIgnoreCase)), Is.False, "deleting the claim row must drop the message from staging");
    }

    async Task<Guid> SeedFailedMessage(
        FailedMessageStatus status,
        DateTime lastModified,
        string queueAddress = null,
        string bodyText = null,
        bool bodyStoredExternally = false)
    {
        var id = Guid.NewGuid();

        await Store(new FailedMessageEntity
        {
            UniqueMessageId = id,
            Status = status,
            StatusChangedAt = lastModified,
            LastModified = lastModified,
            NumberOfProcessingAttempts = 1,
            FirstTimeOfFailure = lastModified,
            LastTimeOfFailure = lastModified,
            LastAttemptedAt = lastModified,
            IsSystemMessage = false,
            HeadersJson = "{}",
            BodyText = bodyText,
            BodyStoredExternally = bodyStoredExternally,
            BodySize = bodyText is null ? 0 : Encoding.UTF8.GetByteCount(bodyText),
            BodyContentType = bodyText is null ? null : "text/plain",
            FailingEndpointAddress = queueAddress
        });

        return id;
    }

    Task SeedRetryClaim(Guid uniqueMessageId, Guid? retryBatchId = null) =>
        Store(new FailedMessageRetryEntity
        {
            UniqueMessageId = uniqueMessageId,
            RetryBatchId = retryBatchId ?? Guid.NewGuid(),
            StageAttempts = 0
        });
}