namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Implementation.BodyStorage;
using ServiceControl.Persistence.EFCore.Infrastructure;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
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
        FileSystem => new FileSystemBodyStoragePersistence(new FileSystemBodyStorageSettings
        {
            StoragePath = tempDir,
            MinCompressionSize = 64
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

        await store.DeleteBodyIfExists(bodyId);

        Assert.That(await store.ReadBody(bodyId), Is.Null);
    }

    [TestCase(InMemory)]
    [TestCase(FileSystem)]
    public void Delete_of_a_missing_body_does_not_throw(string kind)
    {
        var store = CreateStore(kind);

        Assert.DoesNotThrowAsync(() => store.DeleteBodyIfExists(Guid.NewGuid().ToString()));
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

    [TestCase(InMemory, 11)]
    [TestCase(InMemory, 100_000)]
    [TestCase(FileSystem, 11)]
    [TestCase(FileSystem, 100_000)]
    public async Task Returned_stream_never_counts_bytes_it_will_not_yield(string kind, int bodyLength)
    {
        var store = CreateStore(kind);
        var bodyId = Guid.NewGuid().ToString();
        var body = Encoding.UTF8.GetBytes(new string('a', bodyLength));

        await store.WriteBody(bodyId, body, "text/plain");

        var result = await store.ReadBody(bodyId);

        Assert.That(result, Is.Not.Null);
        using (result.Stream)
        {
            if (result.Stream.CanSeek)
            {
                // ASP.NET Core sets the response Content-Length from Stream.Length outright, but
                // copies only from the current position onwards, so anything ahead of the position
                // is promised to the client and never sent.
                Assert.That(result.Stream.Length, Is.EqualTo(body.Length));
            }

            Assert.That(ReadAll(result.Stream), Is.EqualTo(body));
        }
    }

    [Test]
    public async Task Provisioning_creates_the_filesystem_storage_directory()
    {
        var path = Path.Combine(tempDir, "nested", "bodies");
        Assert.That(Directory.Exists(path), Is.False);

        await new FileSystemBodyStorageInstaller(new FileSystemBodyStorageSettings { StoragePath = path }).Provision();

        Assert.That(Directory.Exists(path), Is.True);
        Assert.That(Directory.GetFileSystemEntries(path), Is.Empty);
    }

    [Test]
    public async Task Provisioning_keeps_existing_bodies()
    {
        var existing = Path.Combine(tempDir, "existing");
        await File.WriteAllTextAsync(existing, "body");

        await new FileSystemBodyStorageInstaller(new FileSystemBodyStorageSettings { StoragePath = tempDir }).Provision();

        Assert.That(Directory.GetFileSystemEntries(tempDir), Is.EqualTo(new[] { existing }));
    }

    [Test]
    public async Task Provisioning_fails_with_the_path_when_it_is_a_file()
    {
        var path = Path.Combine(tempDir, "file");
        await File.WriteAllTextAsync(path, "x");

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => new FileSystemBodyStorageInstaller(new FileSystemBodyStorageSettings { StoragePath = path }).Provision());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex.Message, Does.Contain(path));
            Assert.That(ex.InnerException, Is.InstanceOf<IOException>());
        }
    }

    [Test]
    [UnsupportedOSPlatform("windows")]
    public void Provisioning_fails_with_the_path_when_the_directory_is_not_writable()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("UnixFileMode is not supported on Windows.");
        }

        var path = Path.Combine(tempDir, "readonly");
        Directory.CreateDirectory(path);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        try
        {
            if (CanWriteTo(path))
            {
                Assert.Ignore("The current account bypasses directory permissions.");
            }

            var ex = Assert.ThrowsAsync<InvalidOperationException>(() => new FileSystemBodyStorageInstaller(new FileSystemBodyStorageSettings { StoragePath = path }).Provision());

            using (Assert.EnterMultipleScope())
            {
                Assert.That(ex.Message, Does.Contain(path));
                Assert.That(ex.InnerException, Is.InstanceOf<UnauthorizedAccessException>());
            }
        }
        finally
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    static bool CanWriteTo(string path)
    {
        try
        {
            using var _ = new FileStream(Path.Combine(path, Path.GetRandomFileName()), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
