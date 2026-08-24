namespace ServiceControl.UnitTests.BodyStorage;

using System;
using System.IO;
using NUnit.Framework;
using ServiceControl.Operations.BodyStorage;
using ServiceControl.Persistence.Infrastructure;

[TestFixture]
public class MessageBodyResultTests
{
    [TestCase(MessageBodyState.NotFound)]
    [TestCase(MessageBodyState.Empty)]
    [TestCase(MessageBodyState.Unavailable)]
    public void Body_is_not_accessible_without_content(MessageBodyState state)
    {
        var result = state switch
        {
            MessageBodyState.NotFound => MessageBodyResult.NotFound(),
            MessageBodyState.Empty => MessageBodyResult.Empty(),
            MessageBodyState.Unavailable => MessageBodyResult.Unavailable(),
            MessageBodyState.Available => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };

        Assert.Throws<InvalidOperationException>(() => _ = result.Content);
    }

    [Test]
    public void Body_is_accessible_when_available()
    {
        var content = new MessageBodyStreamContent(Stream.Null, "text/plain", 1, DataVersion.FromToken("etag"));
        var result = MessageBodyResult.Available(content);

        Assert.That(result.Content, Is.SameAs(content));
    }

    [Test]
    public void Available_rejects_null() =>
        Assert.Throws<ArgumentNullException>(() => MessageBodyResult.Available(null));
}