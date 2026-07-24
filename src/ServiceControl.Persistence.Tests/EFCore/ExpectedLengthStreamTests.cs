namespace ServiceControl.Persistence.Tests;

using System.IO;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Infrastructure;

[TestFixture]
class ExpectedLengthStreamTests
{
    [Test]
    public void Allows_a_stream_with_the_expected_length()
    {
        using var stream = new ExpectedLengthStream(new MemoryStream([1, 2, 3]), 3);

        Assert.That(ReadAll(stream), Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public void Throws_when_a_stream_ends_before_the_expected_length()
    {
        using var stream = new ExpectedLengthStream(new MemoryStream([1, 2]), 3);

        Assert.That(() => ReadAll(stream), Throws.InvalidOperationException);
    }

    [Test]
    public void Throws_when_a_stream_exceeds_the_expected_length()
    {
        using var stream = new ExpectedLengthStream(new MemoryStream([1, 2, 3]), 2);

        Assert.That(() => ReadAll(stream), Throws.InvalidOperationException);
    }

    static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}