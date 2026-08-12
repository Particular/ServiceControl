namespace ServiceControl.Persistence.Tests;

using System;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Entities;

class FailedMessageRetryBodyDataStoreTests : ErrorIngestionTestBase
{
    static readonly DateTime Noon = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task GetFailedMessageBody_returns_inline_BodyText_bytes()
    {
        var id = await SeedFailedMessage(bodyText: "hello body");

        var body = await FailedMessageRetryStore.GetFailedMessageBody(id.ToString());

        Assert.That(body, Is.EqualTo(Encoding.UTF8.GetBytes("hello body")));
    }

    [Test]
    public async Task GetFailedMessageBody_throws_when_the_body_is_unavailable()
    {
        var id = await SeedFailedMessage(bodyStoredExternally: true);

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            FailedMessageRetryStore.GetFailedMessageBody(id.ToString()));
    }

    [Test]
    public async Task GetFailedMessageBody_returns_external_storage_body_when_BodyStoredExternally()
    {
        var expected = Encoding.UTF8.GetBytes("external body payload");
        var id = await SeedFailedMessage(bodyStoredExternally: true);

        await RecordedBodies.WriteBody(id.ToString(), expected, "text/plain");

        var body = await FailedMessageRetryStore.GetFailedMessageBody(id.ToString());

        Assert.That(body, Is.EqualTo(expected));
    }

    async Task<Guid> SeedFailedMessage(string bodyText = null, bool bodyStoredExternally = false)
    {
        var id = Guid.NewGuid();

        await Store(new FailedMessageEntity
        {
            UniqueMessageId = id,
            Status = FailedMessageStatus.RetryIssued,
            StatusChangedAt = Noon,
            LastModified = Noon,
            NumberOfProcessingAttempts = 1,
            FirstTimeOfFailure = Noon,
            LastTimeOfFailure = Noon,
            LastAttemptedAt = Noon,
            IsSystemMessage = false,
            HeadersJson = "{}",
            BodyText = bodyText,
            BodyStoredExternally = bodyStoredExternally,
            BodySize = bodyText is null ? 0 : Encoding.UTF8.GetByteCount(bodyText),
            BodyContentType = bodyText is null ? null : "text/plain"
        });

        return id;
    }
}
