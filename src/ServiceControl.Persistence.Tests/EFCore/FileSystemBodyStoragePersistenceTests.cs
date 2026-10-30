namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

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
    public async Task Provisioning_creates_the_storage_directory()
    {
        var path = Path.Combine(tempDir, "nested", "bodies");
        var installer = new FileSystemBodyStorageInstaller(new FileSystemBodyStorageSettings { StoragePath = path });

        await installer.Provision();
        await installer.Provision();

        Assert.That(Directory.Exists(path), Is.True);
    }

    [Test]
    public void Provisioning_honors_cancellation()
    {
        var path = Path.Combine(tempDir, "cancelled");
        var installer = new FileSystemBodyStorageInstaller(new FileSystemBodyStorageSettings { StoragePath = path });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => installer.Provision(cancellation.Token));
        Assert.That(Directory.Exists(path), Is.False);
    }

    [Test]
    public async Task Reading_when_the_storage_directory_is_gone_finds_nothing()
    {
        Directory.Delete(tempDir, recursive: true);

        Assert.That(await store.ReadBody("body-1"), Is.Null);

        Directory.CreateDirectory(tempDir);
    }

    static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
