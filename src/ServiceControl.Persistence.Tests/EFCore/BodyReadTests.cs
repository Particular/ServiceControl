namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Operations.BodyStorage;

class BodyReadTests : ErrorIngestionTestBase
{
    const int Cap = 64;

    [SetUp]
    public void ShrinkTheBodyCap() => EFSettings.MaxBodySizeToStore = Cap;

    [Test]
    public async Task Fetches_an_inline_text_body()
    {
        var failure = new IngestedFailure { ContentType = "text/xml", Body = Encoding.UTF8.GetBytes("<order>1</order>") };
        await Ingest(failure);

        var result = await Fetch(failure.UniqueMessageIdString);

        Assert.That(result, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasResult, Is.True);
            Assert.That(result.ContentType, Is.EqualTo("text/xml"));
            Assert.That(Encoding.UTF8.GetString(ReadAll(result.Stream)), Is.EqualTo("<order>1</order>"));
        }
    }

    [Test]
    public async Task Fetches_an_external_binary_body()
    {
        var body = BitConverter.GetBytes(0xDEADBEEF);
        var failure = new IngestedFailure { ContentType = "application/octet-stream", Body = body };
        await Ingest(failure);

        var result = await Fetch(failure.UniqueMessageIdString);

        Assert.That(result, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasResult, Is.True);
            Assert.That(result.ContentType, Is.EqualTo("application/octet-stream"));
            Assert.That(ReadAll(result.Stream), Is.EqualTo(body));
        }
    }

    [Test]
    public async Task Fetches_the_whole_body_for_large_text_not_the_inline_prefix()
    {
        var body = Encoding.UTF8.GetBytes(new string('a', Cap * 2));
        var failure = new IngestedFailure { Body = body };
        await Ingest(failure);

        var result = await Fetch(failure.UniqueMessageIdString);

        Assert.That(result, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasResult, Is.True);
            Assert.That(ReadAll(result.Stream), Is.EqualTo(body), "external storage is authoritative, not the inline search prefix");
        }
    }

    [Test]
    public async Task Fetches_by_message_id()
    {
        var failure = new IngestedFailure { Body = Encoding.UTF8.GetBytes("<order>1</order>") };
        await Ingest(failure);

        var result = await Fetch(failure.MessageId);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.HasResult, Is.True);
    }

    [Test]
    public async Task Reports_no_body_for_an_empty_body()
    {
        var failure = new IngestedFailure { Body = [] };
        await Ingest(failure);

        var result = await Fetch(failure.UniqueMessageIdString);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.HasResult, Is.False);
    }

    [Test]
    public async Task Returns_null_for_an_unknown_message()
    {
        var result = await Fetch(Guid.NewGuid().ToString());

        Assert.That(result, Is.Null);
    }

    async Task<MessageBodyStreamResult> Fetch(string bodyId)
    {
        using var scope = ServiceProvider.CreateScope();
        var bodyStorage = scope.ServiceProvider.GetRequiredService<IBodyStorage>();
        return await bodyStorage.TryFetch(bodyId);
    }

    static byte[] ReadAll(Stream stream)
    {
        using (stream)
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }
}
