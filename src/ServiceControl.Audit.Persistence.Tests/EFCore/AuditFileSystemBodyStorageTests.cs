namespace ServiceControl.Audit.Persistence.Tests.EFCore;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.Implementation.BodyStorage;

[TestFixture]
class AuditFileSystemBodyStorageTests
{
    const string BodyId = "6f0e5a47-6c9b-4a5c-9d86-3b2d1c8a7e10";
    const string BodyA = "a0000000-0000-4000-8000-00000000000a";
    const string BodyB = "b0000000-0000-4000-8000-00000000000b";
    const string BodyC = "c0000000-0000-4000-8000-00000000000c";
    const string BodyD = "d0000000-0000-4000-8000-00000000000d";

    string tempDir;
    string outsideDir;
    AuditFileSystemBodyStorage store;

    [SetUp]
    public void SetUp()
    {
        tempDir = Directory.CreateTempSubdirectory("sc-body-tests-").FullName;
        outsideDir = Directory.CreateTempSubdirectory("sc-body-outside-").FullName;
        store = new AuditFileSystemBodyStorage(new FileSystemBodyStorageSettings { StoragePath = tempDir, MinCompressionSize = 64 });
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(tempDir, recursive: true);
        Directory.Delete(outsideDir, recursive: true);
    }

    static readonly DateTime Hour10 = new(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Hour11 = Hour10.AddHours(1);

    [TestCase(11)]
    [TestCase(100_000)]
    public async Task Round_trips_compressed_and_uncompressed_bodies(int bodyLength)
    {
        var body = Encoding.UTF8.GetBytes(new string('a', bodyLength));
        await store.WriteBody(BodyId, Hour10, body, "application/json");

        var result = await store.ReadBody(BodyId, Hour10);

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
    public async Task Body_is_stored_in_the_directory_of_its_explicit_hour()
    {
        await store.WriteBody(BodyId, Hour10, Encoding.UTF8.GetBytes("payload"), "text/plain");

        Assert.That(Relative(), Is.EqualTo(new[] { Path.Combine("2026102010", $"{BodyId}.body") }));
        var result = await store.ReadBody(BodyId, Hour10);
        Assert.That(result, Is.Not.Null);
        result.Stream.Dispose();
    }

    [Test]
    public async Task Bodies_with_the_same_id_in_different_hours_are_distinct()
    {
        await store.WriteBody(BodyId, Hour10, Encoding.UTF8.GetBytes("ten"), "text/plain");
        await store.WriteBody(BodyId, Hour11, Encoding.UTF8.GetBytes("eleven"), "text/plain");

        using var ten = (await store.ReadBody(BodyId, Hour10)).Stream;
        using var eleven = (await store.ReadBody(BodyId, Hour11)).Stream;

        Assert.Multiple(() =>
        {
            Assert.That(ReadAll(ten), Is.EqualTo(Encoding.UTF8.GetBytes("ten")));
            Assert.That(ReadAll(eleven), Is.EqualTo(Encoding.UTF8.GetBytes("eleven")));
        });
    }

    [Test]
    public async Task Read_for_another_hour_finds_nothing()
    {
        await store.WriteBody(BodyA, Hour10, Encoding.UTF8.GetBytes("payload"), "text/plain");

        Assert.That(await store.ReadBody(BodyA, Hour11), Is.Null);
        Assert.That(await store.ReadBody(BodyA, Hour10.AddDays(-1)), Is.Null);
    }

    [Test]
    public async Task Read_with_an_unspecified_kind_finds_the_body_of_the_same_clock_hour()
    {
        await store.WriteBody(BodyA, Hour10, Encoding.UTF8.GetBytes("payload"), "text/plain");

        var result = await store.ReadBody(BodyA, DateTime.SpecifyKind(Hour10, DateTimeKind.Unspecified));

        Assert.That(result, Is.Not.Null);
        result.Stream.Dispose();
    }

    [Test]
    public async Task Rewriting_a_body_keeps_the_first_write()
    {
        await store.WriteBody(BodyA, Hour10, Encoding.UTF8.GetBytes("original"), "text/plain");
        await store.WriteBody(BodyA, Hour10, Encoding.UTF8.GetBytes("different"), "text/plain");

        using var result = (await store.ReadBody(BodyA, Hour10)).Stream;

        Assert.That(ReadAll(result), Is.EqualTo(Encoding.UTF8.GetBytes("original")));
    }

    [Test]
    public async Task Delete_for_hour_removes_only_that_hour()
    {
        var body = Encoding.UTF8.GetBytes("payload");
        await store.WriteBody(BodyA, Hour10, body, "text/plain");
        await store.WriteBody(BodyB, Hour10, body, "text/plain");
        await store.WriteBody(BodyA, Hour11, body, "text/plain");
        var unrelated = Path.Combine(tempDir, "flat.body");
        await File.WriteAllTextAsync(unrelated, "keep");

        await store.DeleteBodiesForHour(Hour10);

        Assert.Multiple(() =>
        {
            Assert.That(Relative(), Is.EquivalentTo(new[] { Path.Combine("2026102011", $"{BodyA}.body"), "flat.body" }));
            Assert.That(Directory.Exists(Path.Combine(tempDir, "2026102010")), Is.False);
        });
    }

    [Test]
    public async Task Delete_for_hour_also_removes_temp_files_in_that_hour_only()
    {
        await store.WriteBody(BodyA, Hour10, Encoding.UTF8.GetBytes("payload"), "text/plain");
        await store.WriteBody(BodyA, Hour11, Encoding.UTF8.GetBytes("payload"), "text/plain");
        await File.WriteAllTextAsync(Path.Combine(tempDir, "2026102010", $"{BodyA}.body.abc.tmp"), "in progress");
        var otherTemp = Path.Combine(tempDir, "2026102011", $"{BodyA}.body.abc.tmp");
        await File.WriteAllTextAsync(otherTemp, "in progress");
        var rootFile = Path.Combine(tempDir, "unrelated.txt");
        await File.WriteAllTextAsync(rootFile, "keep");

        await store.DeleteBodiesForHour(Hour10);

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(Path.Combine(tempDir, "2026102010")), Is.False);
            Assert.That(File.Exists(otherTemp), Is.True);
            Assert.That(File.Exists(rootFile), Is.True);
        });
    }

    [Test]
    public async Task Delete_for_day_removes_every_hour_of_that_day_and_no_other()
    {
        var body = Encoding.UTF8.GetBytes("payload");
        await store.WriteBody(BodyA, Hour10.Date, body, "text/plain");
        await store.WriteBody(BodyB, Hour10.Date.AddHours(23), body, "text/plain");
        await store.WriteBody(BodyC, Hour10.Date.AddDays(1), body, "text/plain");
        await store.WriteBody(BodyD, Hour10.Date.AddHours(-1), body, "text/plain");
        Directory.CreateDirectory(Path.Combine(tempDir, "20261020-unrelated"));

        await store.DeleteBodiesForDay(Hour10);

        Assert.Multiple(() =>
        {
            Assert.That(Relative(), Is.EquivalentTo(new[] { Path.Combine("2026102100", $"{BodyC}.body"), Path.Combine("2026101923", $"{BodyD}.body") }));
            Assert.That(Directory.Exists(Path.Combine(tempDir, "20261020-unrelated")), Is.True);
        });
    }

    [Test]
    public void Delete_succeeds_when_nothing_is_stored()
    {
        Assert.DoesNotThrowAsync(() => store.DeleteBodiesForHour(Hour10));
        Assert.DoesNotThrowAsync(() => store.DeleteBodiesForDay(Hour10));
    }

    [Test]
    public async Task Delete_honors_cancellation()
    {
        await store.WriteBody(BodyA, Hour10, Encoding.UTF8.GetBytes("payload"), "text/plain");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Assert.ThrowsAsync<OperationCanceledException>(() => store.DeleteBodiesForHour(Hour10, cts.Token));
        Assert.ThrowsAsync<OperationCanceledException>(() => store.DeleteBodiesForDay(Hour10, cts.Token));
        Assert.That(Relative(), Has.Length.EqualTo(1));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("body-1")]
    [TestCase("../6f0e5a47-6c9b-4a5c-9d86-3b2d1c8a7e10")]
    [TestCase("..\\6f0e5a47-6c9b-4a5c-9d86-3b2d1c8a7e10")]
    [TestCase("/etc/6f0e5a47-6c9b-4a5c-9d86-3b2d1c8a7e10")]
    [TestCase("C:\\temp\\6f0e5a47-6c9b-4a5c-9d86-3b2d1c8a7e10")]
    [TestCase("failedimport-6f0e5a47-6c9b-4a5c-9d86-3b2d1c8a7e10")]
    [TestCase("outside-relative")]
    [TestCase("outside-rooted")]
    public async Task Invalid_body_ids_are_rejected_without_touching_the_file_system(string bodyId)
    {
        var outsideFile = Path.Combine(outsideDir, "target.body");
        await File.WriteAllTextAsync(outsideFile, "keep");
        bodyId = bodyId switch
        {
            "outside-relative" => Path.GetRelativePath(Path.Combine(tempDir, "2026102010"), Path.Combine(outsideDir, "target")),
            "outside-rooted" => Path.Combine(outsideDir, "target"),
            _ => bodyId
        };

        var write = Assert.ThrowsAsync<ArgumentException>(() => store.WriteBody(bodyId, Hour10, Encoding.UTF8.GetBytes("payload"), "text/plain"));
        var read = Assert.ThrowsAsync<ArgumentException>(() => store.ReadBody(bodyId, Hour10));

        Assert.Multiple(() =>
        {
            Assert.That(write.ParamName, Is.EqualTo("bodyId"));
            Assert.That(read.ParamName, Is.EqualTo("bodyId"));
            Assert.That(Directory.GetFileSystemEntries(tempDir), Is.Empty);
            Assert.That(Directory.GetFileSystemEntries(outsideDir), Is.EqualTo(new[] { outsideFile }));
            Assert.That(File.ReadAllText(outsideFile), Is.EqualTo("keep"));
        });
    }

    string[] Relative() => Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(tempDir, f)).ToArray();

    static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
