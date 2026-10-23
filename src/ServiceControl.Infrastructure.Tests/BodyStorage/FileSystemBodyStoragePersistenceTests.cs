namespace ServiceControl.Infrastructure.Tests.BodyStorage;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.BodyStorage;

[TestFixture]
class FileSystemBodyStoragePersistenceTests
{
    string tempDir;
    FileSystemBodyStoragePersistence store;

    [SetUp]
    public void SetUp()
    {
        tempDir = Directory.CreateTempSubdirectory("sc-body-tests-").FullName;
        store = new FileSystemBodyStoragePersistence(new FileSystemBodyStorageSettings { StoragePath = tempDir, MinCompressionSize = 64 });
    }

    [TearDown]
    public void TearDown() => Directory.Delete(tempDir, recursive: true);

    [TestCase(11)]
    [TestCase(100_000)]
    public async Task Round_trips_compressed_and_uncompressed_bodies(int bodyLength)
    {
        var body = Encoding.UTF8.GetBytes(new string('a', bodyLength));
        await store.WriteBody("body-1", body, "application/json");

        var result = await store.ReadBody("body-1");

        Assert.That(result, Is.Not.Null);
        using (result.Stream)
        {
            Assert.Multiple(() =>
            {
                Assert.That(ReadAll(result.Stream), Is.EqualTo(body));
                Assert.That(result.ContentType, Is.EqualTo("application/json"));
                Assert.That(result.BodySize, Is.EqualTo(bodyLength));
                Assert.That(result.Stream.CanSeek, Is.False, "a seekable stream would advertise the header bytes in its Length");
            });
        }
    }

    [Test]
    public async Task Rewriting_a_body_keeps_the_first_write()
    {
        await store.WriteBody("body-1", Encoding.UTF8.GetBytes("original"), "text/plain");
        await store.WriteBody("body-1", Encoding.UTF8.GetBytes("different"), "text/plain");

        var result = await store.ReadBody("body-1");

        using (result.Stream)
        {
            Assert.That(ReadAll(result.Stream), Is.EqualTo(Encoding.UTF8.GetBytes("original")));
        }
    }

    [Test]
    public async Task Missing_body_reads_as_null_and_deletes_quietly()
    {
        Assert.That(await store.ReadBody("missing"), Is.Null);
        Assert.DoesNotThrowAsync(() => store.DeleteBodyIfExists("missing"));
    }

    [Test]
    public async Task Delete_with_prefix_removes_only_bodies_with_that_prefix()
    {
        var body = Encoding.UTF8.GetBytes("payload");
        await store.WriteBody("2026102010-a", body, "text/plain");
        await store.WriteBody("2026102010-b", body, "text/plain");
        await store.WriteBody("2026102011-a", body, "text/plain");

        await store.DeleteBodiesWithPrefix("2026102010");

        Assert.That(await store.ReadBody("2026102010-a"), Is.Null);
        Assert.That(await store.ReadBody("2026102010-b"), Is.Null);
        Assert.That(File.Exists(Path.Combine(tempDir, "2026102011-a.body")), Is.True);
    }

    [Test]
    public async Task Delete_with_prefix_leaves_non_body_files()
    {
        var other = Path.Combine(tempDir, $"2026102010-a.body.{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(other, "x");

        await store.DeleteBodiesWithPrefix("2026102010");

        Assert.That(File.Exists(other), Is.True);
    }

    [TestCase("prefix", "prefix-a", "PREFIX-b")]
    [TestCase("<", "keep-a", "keep-b")]
    public async Task Delete_with_prefix_uses_literal_ordinal_matching(string prefix, string firstId, string secondId)
    {
        var body = Encoding.UTF8.GetBytes("payload");
        await store.WriteBody(firstId, body, "text/plain");
        await store.WriteBody(secondId, body, "text/plain");

        await store.DeleteBodiesWithPrefix(prefix);

        Assert.That(File.Exists(Path.Combine(tempDir, $"{firstId}.body")), Is.EqualTo(!firstId.StartsWith(prefix, StringComparison.Ordinal)));
        Assert.That(File.Exists(Path.Combine(tempDir, $"{secondId}.body")), Is.True);
    }

    [Test]
    public void Delete_with_prefix_succeeds_when_the_storage_directory_is_missing()
    {
        var missing = new FileSystemBodyStoragePersistence(new FileSystemBodyStorageSettings { StoragePath = Path.Combine(tempDir, "nope") });

        Assert.DoesNotThrowAsync(() => missing.DeleteBodiesWithPrefix("2026102010"));
    }

    [TestCase("")]
    [TestCase("  ")]
    [TestCase("*")]
    [TestCase("a?")]
    [TestCase("../x")]
    [TestCase("a/b")]
    [TestCase("a\\b")]
    public async Task Delete_with_prefix_rejects_prefixes_that_could_widen_the_match(string prefix)
    {
        await store.WriteBody("keep", Encoding.UTF8.GetBytes("payload"), "text/plain");

        Assert.ThrowsAsync<ArgumentException>(() => store.DeleteBodiesWithPrefix(prefix));
        Assert.That(File.Exists(Path.Combine(tempDir, "keep.body")), Is.True);
    }

    [Test]
    public async Task Delete_with_prefix_honors_cancellation()
    {
        await store.WriteBody("a-1", Encoding.UTF8.GetBytes("payload"), "text/plain");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Assert.ThrowsAsync<OperationCanceledException>(() => store.DeleteBodiesWithPrefix("a", cts.Token));
        Assert.That(Directory.EnumerateFiles(tempDir).Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Provisioning_creates_the_storage_directory()
    {
        var path = Path.Combine(tempDir, "nested", "bodies");

        await BodyStorageProvisioner.Provision(new FileSystemBodyStorageSettings { StoragePath = path });
        await BodyStorageProvisioner.Provision(new FileSystemBodyStorageSettings { StoragePath = path });

        Assert.That(Directory.Exists(path), Is.True);
    }

    static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
