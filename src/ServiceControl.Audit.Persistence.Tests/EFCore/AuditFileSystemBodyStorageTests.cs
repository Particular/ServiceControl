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
    string tempDir;
    AuditFileSystemBodyStorage store;

    [SetUp]
    public void SetUp()
    {
        tempDir = Directory.CreateTempSubdirectory("sc-body-tests-").FullName;
        store = new AuditFileSystemBodyStorage(new FileSystemBodyStorageSettings { StoragePath = tempDir, MinCompressionSize = 64 });
    }

    [TearDown]
    public void TearDown() => Directory.Delete(tempDir, recursive: true);

    static readonly DateTime Hour10 = new(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Hour11 = Hour10.AddHours(1);

    [TestCase(11)]
    [TestCase(100_000)]
    public async Task Round_trips_compressed_and_uncompressed_bodies(int bodyLength)
    {
        var body = Encoding.UTF8.GetBytes(new string('a', bodyLength));
        await store.WriteBody("body-1", Hour10, body, "application/json");

        var result = await store.ReadBody("body-1", Hour10);

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
    public async Task Body_is_stored_in_the_directory_of_its_explicit_hour_whatever_the_id()
    {
        await store.WriteBody("opaque", Hour10, Encoding.UTF8.GetBytes("payload"), "text/plain");

        Assert.That(Relative(), Is.EqualTo(new[] { Path.Combine("2026102010", "opaque.body") }));
        var result = await store.ReadBody("opaque", Hour10);
        Assert.That(result, Is.Not.Null);
        result.Stream.Dispose();
    }

    [Test]
    public async Task Bodies_with_the_same_id_in_different_hours_are_distinct()
    {
        await store.WriteBody("same", Hour10, Encoding.UTF8.GetBytes("ten"), "text/plain");
        await store.WriteBody("same", Hour11, Encoding.UTF8.GetBytes("eleven"), "text/plain");

        using var ten = (await store.ReadBody("same", Hour10)).Stream;
        using var eleven = (await store.ReadBody("same", Hour11)).Stream;

        Assert.Multiple(() =>
        {
            Assert.That(ReadAll(ten), Is.EqualTo(Encoding.UTF8.GetBytes("ten")));
            Assert.That(ReadAll(eleven), Is.EqualTo(Encoding.UTF8.GetBytes("eleven")));
        });
    }

    [Test]
    public async Task Read_for_another_hour_finds_nothing()
    {
        await store.WriteBody("a", Hour10, Encoding.UTF8.GetBytes("payload"), "text/plain");

        Assert.That(await store.ReadBody("a", Hour11), Is.Null);
        Assert.That(await store.ReadBody("a", Hour10.AddDays(-1)), Is.Null);
    }

    [Test]
    public async Task Read_with_an_unspecified_kind_finds_the_body_of_the_same_clock_hour()
    {
        await store.WriteBody("a", Hour10, Encoding.UTF8.GetBytes("payload"), "text/plain");

        var result = await store.ReadBody("a", DateTime.SpecifyKind(Hour10, DateTimeKind.Unspecified));

        Assert.That(result, Is.Not.Null);
        result.Stream.Dispose();
    }

    [Test]
    public async Task Rewriting_a_body_keeps_the_first_write()
    {
        await store.WriteBody("a", Hour10, Encoding.UTF8.GetBytes("original"), "text/plain");
        await store.WriteBody("a", Hour10, Encoding.UTF8.GetBytes("different"), "text/plain");

        using var result = (await store.ReadBody("a", Hour10)).Stream;

        Assert.That(ReadAll(result), Is.EqualTo(Encoding.UTF8.GetBytes("original")));
    }

    [Test]
    public async Task Delete_for_hour_removes_only_that_hour()
    {
        var body = Encoding.UTF8.GetBytes("payload");
        await store.WriteBody("a", Hour10, body, "text/plain");
        await store.WriteBody("b", Hour10, body, "text/plain");
        await store.WriteBody("a", Hour11, body, "text/plain");
        var unrelated = Path.Combine(tempDir, "flat.body");
        await File.WriteAllTextAsync(unrelated, "keep");

        await store.DeleteBodiesForHour(Hour10);

        Assert.Multiple(() =>
        {
            Assert.That(Relative(), Is.EquivalentTo(new[] { Path.Combine("2026102011", "a.body"), "flat.body" }));
            Assert.That(Directory.Exists(Path.Combine(tempDir, "2026102010")), Is.False);
        });
    }

    [Test]
    public async Task Delete_for_hour_also_removes_temp_files_in_that_hour_only()
    {
        await store.WriteBody("a", Hour10, Encoding.UTF8.GetBytes("payload"), "text/plain");
        await store.WriteBody("a", Hour11, Encoding.UTF8.GetBytes("payload"), "text/plain");
        await File.WriteAllTextAsync(Path.Combine(tempDir, "2026102010", "a.body.abc.tmp"), "in progress");
        var otherTemp = Path.Combine(tempDir, "2026102011", "a.body.abc.tmp");
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
        await store.WriteBody("a", Hour10.Date, body, "text/plain");
        await store.WriteBody("b", Hour10.Date.AddHours(23), body, "text/plain");
        await store.WriteBody("c", Hour10.Date.AddDays(1), body, "text/plain");
        await store.WriteBody("d", Hour10.Date.AddHours(-1), body, "text/plain");
        Directory.CreateDirectory(Path.Combine(tempDir, "20261020-unrelated"));

        await store.DeleteBodiesForDay(Hour10);

        Assert.Multiple(() =>
        {
            Assert.That(Relative(), Is.EquivalentTo(new[] { Path.Combine("2026102100", "c.body"), Path.Combine("2026101923", "d.body") }));
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
        await store.WriteBody("a", Hour10, Encoding.UTF8.GetBytes("payload"), "text/plain");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Assert.ThrowsAsync<OperationCanceledException>(() => store.DeleteBodiesForHour(Hour10, cts.Token));
        Assert.ThrowsAsync<OperationCanceledException>(() => store.DeleteBodiesForDay(Hour10, cts.Token));
        Assert.That(Relative(), Has.Length.EqualTo(1));
    }

    string[] Relative() => Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(tempDir, f)).ToArray();

    static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
