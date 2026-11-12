namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.Tests;

[TestFixture]
[NonParallelizable]
class FailedMessageBodyMigrationTests : MigrationAcceptanceTest
{
    [Test]
    public async Task A_small_text_body_stays_inline_and_is_never_written_externally()
    {
        var failure = new IngestedFailure { ContentType = "text/xml" };
        var content = Encoding.UTF8.GetBytes("<order>1</order>");
        await SeedSourceWithBody(failure.ToFailedMessage(), content, "text/xml");

        await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var row = await GetFailedMessage(failure.UniqueMessageId);
        var fetched = await BodyStorage.TryFetch(failure.UniqueMessageId.ToString());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.BodyText, Is.EqualTo("<order>1</order>"));
            Assert.That(row.BodyStoredExternally, Is.False);
            Assert.That(row.BodySize, Is.EqualTo(content.Length));
            Assert.That(row.BodyContentType, Is.EqualTo("text/xml"));
            Assert.That(RecordedBodies.Written, Is.Empty);
            Assert.That(fetched.State, Is.EqualTo(MessageBodyState.Available));
            Assert.That(fetched.Content.ContentType, Is.EqualTo("text/xml"));
            Assert.That(await ReadAll(fetched.Content.Stream), Is.EqualTo(content));
        }
    }

    [Test]
    public async Task A_large_text_body_leaves_a_search_prefix_inline_and_writes_the_whole_body_externally()
    {
        var failure = new IngestedFailure { ContentType = "application/json" };
        var text = new string('a', EFSettings.BodyStorage.MaxBodySizeToStore * 2);
        var content = Encoding.UTF8.GetBytes(text);
        await SeedSourceWithBody(failure.ToFailedMessage(), content, "application/json");

        await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.BodyText, Is.Not.Null, "an over-cap text body still keeps a prefix inline so that message search can find it");
            Assert.That(text, Does.StartWith(row.BodyText));
            Assert.That(row.BodyText.Length, Is.EqualTo(EFSettings.BodyStorage.MaxBodySizeToStore));
            Assert.That(row.BodyStoredExternally, Is.True);
            Assert.That(row.BodySize, Is.EqualTo(content.Length));
        }

        var written = RecordedBodies.Written.Single();
        var fetched = await BodyStorage.TryFetch(failure.UniqueMessageId.ToString());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(written.BodyId, Is.EqualTo(failure.UniqueMessageId.ToString()));
            Assert.That(written.Body, Is.EqualTo(content));
            Assert.That(written.ContentType, Is.EqualTo("application/json"));
            Assert.That(fetched.State, Is.EqualTo(MessageBodyState.Available));
            Assert.That(fetched.Content.ContentType, Is.EqualTo("application/json"));
            Assert.That(await ReadAll(fetched.Content.Stream), Is.EqualTo(content));
        }
    }

    [Test]
    public async Task A_binary_body_goes_external_with_nothing_left_inline()
    {
        var failure = new IngestedFailure { ContentType = "application/x-protobuf" };
        var content = new byte[] { 0x08, 0x00, 0x96, 0x01, 0xFF };
        await SeedSourceWithBody(failure.ToFailedMessage(), content, "application/x-protobuf");

        await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var row = await GetFailedMessage(failure.UniqueMessageId);
        var written = RecordedBodies.Written.Single();
        var fetched = await BodyStorage.TryFetch(failure.UniqueMessageId.ToString());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.BodyText, Is.Null, "there is nothing in a binary payload for message search to index");
            Assert.That(row.BodyStoredExternally, Is.True);
            Assert.That(row.BodySize, Is.EqualTo(content.Length));
            Assert.That(row.BodyContentType, Is.EqualTo("application/x-protobuf"));
            Assert.That(written.BodyId, Is.EqualTo(failure.UniqueMessageId.ToString()));
            Assert.That(written.Body, Is.EqualTo(content));
            Assert.That(written.ContentType, Is.EqualTo("application/x-protobuf"));
            Assert.That(fetched.State, Is.EqualTo(MessageBodyState.Available));
            Assert.That(fetched.Content.ContentType, Is.EqualTo("application/x-protobuf"));
            Assert.That(await ReadAll(fetched.Content.Stream), Is.EqualTo(content));
        }
    }

    // Covers the missing-attachment path (BodyMissing, one read) only; the retried BodyUnreadable path is covered by the loader unit tests.
    [Test]
    public async Task A_message_with_no_body_attachment_is_left_out_entirely()
    {
        var readable = new IngestedFailure();
        var missing = new IngestedFailure();

        await SeedSourceWithBody(readable.ToFailedMessage(), Encoding.UTF8.GetBytes("<order>1</order>"), "text/xml");
        await SeedSource(($"FailedMessages/{missing.UniqueMessageId}", missing.ToFailedMessage()));

        var result = await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var checkpoint = await ReadCheckpoint(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await FindFailedMessage(readable.UniqueMessageId), Is.Not.Null);
            Assert.That(await FindFailedMessage(missing.UniqueMessageId), Is.Null, "a row with no body would dispatch an empty message if anyone retried it");
            Assert.That(result.Skipped, Is.EqualTo(1));
            Assert.That(checkpoint.SkipReasons, Contains.Key(MigrationSkipReason.BodyMissing), "the skip is counted under the reason that tells the operator no retry can fix it");
            Assert.That(checkpoint.SkipReasons, Does.Not.ContainKey(MigrationSkipReason.BodyUnreadable));
        }
    }

    [Test]
    public async Task A_zero_length_body_is_copied_rather_than_treated_as_missing()
    {
        var failure = new IngestedFailure();
        await SeedSourceWithBody(failure.ToFailedMessage(), [], "text/plain");

        var result = await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(row.BodySize, Is.Zero);
            Assert.That(row.BodyText, Is.Null, "an empty body is stored nowhere, which is not the same as being stored externally");
            Assert.That(row.BodyStoredExternally, Is.False);
            Assert.That(RecordedBodies.Written, Is.Empty);
        }
    }

    [Test]
    public async Task The_copied_body_reads_back_through_the_products_own_body_storage()
    {
        var inline = new IngestedFailure { ContentType = "text/xml" };
        var external = new IngestedFailure { ContentType = "application/x-protobuf" };
        var inlineContent = Encoding.UTF8.GetBytes("<order>1</order>");
        var externalContent = new byte[] { 0x08, 0x00, 0x96, 0x01, 0xFF };

        await SeedSourceWithBodies(
            (inline.ToFailedMessage(), inlineContent, "text/xml"),
            (external.ToFailedMessage(), externalContent, "application/x-protobuf"));

        await CopyCategory(MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

        Assert.That(await Fetch(inline.UniqueMessageId), Is.EqualTo(inlineContent));
        Assert.That(await Fetch(external.UniqueMessageId), Is.EqualTo(externalContent), "the body was stored under an id TryFetch does not resolve");
    }

    async Task<byte[]> Fetch(Guid uniqueMessageId)
    {
        var result = await BodyStorage.TryFetch(uniqueMessageId.ToString());

        Assert.That(result.State, Is.EqualTo(MessageBodyState.Available));

        return await ReadAll(result.Content.Stream);
    }

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
