namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

[TestFixture]
class FileSystemBodyStoragePersistenceTests
{
    const string BodyId = "6f0e5a47-6c9b-4a5c-9d86-3b2d1c8a7e10";
    const string MissingBodyId = "0b9a1d52-4d7c-4f3e-8a21-5c6e7f8091a2";

    string tempDir;
    string outsideDir;
    FileSystemBodyStoragePersistence store;

    [SetUp]
    public void SetUp()
    {
        tempDir = Directory.CreateTempSubdirectory("sc-body-tests-").FullName;
        outsideDir = Directory.CreateTempSubdirectory("sc-body-outside-").FullName;
        store = new FileSystemBodyStoragePersistence(new FileSystemBodyStorageSettings { StoragePath = tempDir, MinCompressionSize = 64 });
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(tempDir, recursive: true);
        Directory.Delete(outsideDir, recursive: true);
    }

    [TestCase(11)]
    [TestCase(100_000)]
    public async Task Round_trips_compressed_and_uncompressed_bodies(int bodyLength)
    {
        var body = Encoding.UTF8.GetBytes(new string('a', bodyLength));
        await store.WriteBody(BodyId, body, "application/json");

        var result = await store.ReadBody(BodyId);

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
        await store.WriteBody(BodyId, Encoding.UTF8.GetBytes("original"), "text/plain");
        await store.WriteBody(BodyId, Encoding.UTF8.GetBytes("different"), "text/plain");

        var result = await store.ReadBody(BodyId);

        using (result.Stream)
        {
            Assert.That(ReadAll(result.Stream), Is.EqualTo(Encoding.UTF8.GetBytes("original")));
        }
    }

    [Test]
    public async Task Missing_body_reads_as_null_and_deletes_quietly()
    {
        Assert.That(await store.ReadBody(MissingBodyId), Is.Null);
        Assert.DoesNotThrowAsync(() => store.DeleteBodyIfExists(MissingBodyId));
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

        Assert.That(await store.ReadBody(BodyId), Is.Null);

        Directory.CreateDirectory(tempDir);
    }

    [Test]
    public async Task Failed_import_body_ids_round_trip_and_delete()
    {
        var bodyId = FailedErrorImportEntity.ExternalBodyId(Guid.Parse(BodyId));
        await store.WriteBody(bodyId, Encoding.UTF8.GetBytes("payload"), "text/plain");

        Assert.That(File.Exists(Path.Combine(tempDir, $"{bodyId}.body")), Is.True);
        var result = await store.ReadBody(bodyId);
        Assert.That(result, Is.Not.Null);
        using (result.Stream)
        {
            Assert.That(ReadAll(result.Stream), Is.EqualTo(Encoding.UTF8.GetBytes("payload")));
        }

        await store.DeleteBodyIfExists(bodyId);

        Assert.That(Directory.GetFileSystemEntries(tempDir), Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("body-1")]
    [TestCase("../6f0e5a47-6c9b-4a5c-9d86-3b2d1c8a7e10")]
    [TestCase("..\\6f0e5a47-6c9b-4a5c-9d86-3b2d1c8a7e10")]
    [TestCase("/etc/6f0e5a47-6c9b-4a5c-9d86-3b2d1c8a7e10")]
    [TestCase("C:\\temp\\6f0e5a47-6c9b-4a5c-9d86-3b2d1c8a7e10")]
    [TestCase("failedimport-")]
    [TestCase("failedimport-../x")]
    [TestCase("failedimport-..\\x")]
    [TestCase("failedimport-6f0e5a47-6c9b-4a5c-9d86-3b2d1c8a7e10/x")]
    [TestCase("FAILEDIMPORT-6f0e5a47-6c9b-4a5c-9d86-3b2d1c8a7e10")]
    [TestCase("failedimport-failedimport-6f0e5a47-6c9b-4a5c-9d86-3b2d1c8a7e10")]
    [TestCase("outside-relative")]
    [TestCase("outside-rooted")]
    public async Task Invalid_body_ids_are_rejected_without_touching_the_file_system(string bodyId)
    {
        var outsideFile = Path.Combine(outsideDir, "target.body");
        await File.WriteAllTextAsync(outsideFile, "keep");
        bodyId = bodyId switch
        {
            "outside-relative" => Path.GetRelativePath(tempDir, Path.Combine(outsideDir, "target")),
            "outside-rooted" => Path.Combine(outsideDir, "target"),
            _ => bodyId
        };

        var write = Assert.ThrowsAsync<ArgumentException>(() => store.WriteBody(bodyId, Encoding.UTF8.GetBytes("payload"), "text/plain"));
        var read = Assert.ThrowsAsync<ArgumentException>(() => store.ReadBody(bodyId));
        var delete = Assert.ThrowsAsync<ArgumentException>(() => store.DeleteBodyIfExists(bodyId));

        Assert.Multiple(() =>
        {
            Assert.That(write.ParamName, Is.EqualTo("bodyId"));
            Assert.That(read.ParamName, Is.EqualTo("bodyId"));
            Assert.That(delete.ParamName, Is.EqualTo("bodyId"));
            Assert.That(Directory.GetFileSystemEntries(tempDir), Is.Empty);
            Assert.That(Directory.GetFileSystemEntries(outsideDir), Is.EqualTo(new[] { outsideFile }));
            Assert.That(File.ReadAllText(outsideFile), Is.EqualTo("keep"));
        });
    }

    static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
