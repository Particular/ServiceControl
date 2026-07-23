namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Implementation;
using ServiceControl.Persistence.EFCore.Infrastructure;

[TestFixture]
class BodyStoragePersistenceTests
{
    const string FileSystem = nameof(FileSystem);
    const string InMemory = nameof(InMemory);

    string tempDir;

    [SetUp]
    public void SetUp() => tempDir = Directory.CreateTempSubdirectory("sc-body-tests-").FullName;

    [TearDown]
    public void TearDown()
    {
        try
        {
            Directory.Delete(tempDir, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    IBodyStoragePersistence CreateStore(string kind) => kind switch
    {
        InMemory => new InMemoryBodyStoragePersistence(),
        FileSystem => new FileSystemBodyStoragePersistence(new TestSettings
        {
            ConnectionString = "not-used",
            MessageBodyStoragePath = tempDir,
            MinBodySizeForCompression = 64
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    [TestCase(InMemory)]
    [TestCase(FileSystem)]
    public async Task Round_trips_a_small_uncompressed_body(string kind)
    {
        var store = CreateStore(kind);
        var bodyId = Guid.NewGuid().ToString();
        var body = Encoding.UTF8.GetBytes("hello world");

        await store.WriteBody(bodyId, body, "text/plain");

        var result = await store.ReadBody(bodyId);

        Assert.That(result, Is.Not.Null);
        using (result.Stream)
        {
            Assert.That(ReadAll(result.Stream), Is.EqualTo(body));
        }

        Assert.Multiple(() =>
        {
            Assert.That(result.ContentType, Is.EqualTo("text/plain"));
            Assert.That(result.BodySize, Is.EqualTo(body.Length));
        });
    }

    [TestCase(InMemory)]
    [TestCase(FileSystem)]
    public async Task Round_trips_a_large_body_over_the_compression_threshold(string kind)
    {
        var store = CreateStore(kind);
        var bodyId = Guid.NewGuid().ToString();
        var body = Encoding.UTF8.GetBytes(new string('a', 100_000));

        await store.WriteBody(bodyId, body, "application/json");

        var result = await store.ReadBody(bodyId);

        Assert.That(result, Is.Not.Null);
        using (result.Stream)
        {
            Assert.That(ReadAll(result.Stream), Is.EqualTo(body));
        }

        Assert.That(result.BodySize, Is.EqualTo(body.Length));
    }

    [TestCase(InMemory)]
    [TestCase(FileSystem)]
    public async Task Returns_null_for_a_missing_body(string kind)
    {
        var store = CreateStore(kind);

        Assert.That(await store.ReadBody(Guid.NewGuid().ToString()), Is.Null);
    }

    [TestCase(InMemory)]
    [TestCase(FileSystem)]
    public async Task Delete_removes_the_body(string kind)
    {
        var store = CreateStore(kind);
        var bodyId = Guid.NewGuid().ToString();
        await store.WriteBody(bodyId, Encoding.UTF8.GetBytes("payload"), "text/plain");

        await store.DeleteBody(bodyId);

        Assert.That(await store.ReadBody(bodyId), Is.Null);
    }

    [TestCase(InMemory)]
    [TestCase(FileSystem)]
    public void Delete_of_a_missing_body_does_not_throw(string kind)
    {
        var store = CreateStore(kind);

        Assert.DoesNotThrowAsync(() => store.DeleteBody(Guid.NewGuid().ToString()));
    }

    [TestCase(InMemory)]
    [TestCase(FileSystem)]
    public async Task Rewriting_an_existing_body_keeps_the_first_write(string kind)
    {
        var store = CreateStore(kind);
        var bodyId = Guid.NewGuid().ToString();
        var original = Encoding.UTF8.GetBytes("original");

        await store.WriteBody(bodyId, original, "text/plain");
        await store.WriteBody(bodyId, Encoding.UTF8.GetBytes("different"), "text/plain");

        var result = await store.ReadBody(bodyId);

        Assert.That(result, Is.Not.Null);
        using (result.Stream)
        {
            Assert.That(ReadAll(result.Stream), Is.EqualTo(original), "bodies are immutable, so the first write wins");
        }
    }

    [Test]
    public async Task Provisioning_creates_the_filesystem_storage_directory()
    {
        var path = Path.Combine(tempDir, "nested", "bodies");
        Assert.That(Directory.Exists(path), Is.False);

        await new FileSystemBodyStorageInstaller(new TestSettings
        {
            ConnectionString = "not-used",
            BodyStorageType = BodyStorageType.FileSystem,
            MessageBodyStoragePath = path
        }).Provision();

        Assert.That(Directory.Exists(path), Is.True);
    }

    static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    sealed class TestSettings : EFPersisterSettings;
}
