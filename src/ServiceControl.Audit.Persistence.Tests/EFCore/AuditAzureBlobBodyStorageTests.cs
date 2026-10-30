namespace ServiceControl.Audit.Persistence.Tests.EFCore;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Audit.Persistence.EFCore.Abstractions;
using ServiceControl.Audit.Persistence.EFCore.Implementation.BodyStorage;
using Testcontainers.Azurite;

[TestFixture]
[Platform(Exclude = "Win", Reason = "Azurite has no Windows container image")]
class AzureBlobBodyStorageTests
{
    static AzuriteContainer azurite;

    [OneTimeSetUp]
    public static async Task StartAzurite()
    {
        // Azurite 3.36.0 does not implement the service version the current Azure.Storage.Blobs
        // package negotiates, so the API version check has to be skipped. Support is milestoned for
        // Azurite 3.37.0 (https://github.com/Azure/Azurite/issues/2623); when that ships, move the
        // tag forward and drop --skipApiVersionCheck together.
        azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.36.0")
            .WithCommand("--skipApiVersionCheck")
            .Build();
        await azurite.StartAsync();
    }

    [OneTimeTearDown]
    public static async Task StopAzurite()
    {
        if (azurite != null)
        {
            await azurite.DisposeAsync();
        }
    }

    public async Task<AuditAzureBlobBodyStorage> CreateContainer()
    {
        var settings = new AzureBlobBodyStorageSettings
        {
            MinCompressionSize = 64,
            Authentication = new AzureBlobSharedKeyAuthentication { ConnectionString = azurite.GetConnectionString() },
            ContainerName = $"bodies-{Guid.NewGuid():n}"
        };

        await AuditAzureBlobClientFactory.CreateContainerClient(settings).CreateIfNotExistsAsync();
        return new AuditAzureBlobBodyStorage(settings);
    }

    static readonly DateTime Hour10 = new(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);
    static readonly DateTime Hour11 = Hour10.AddHours(1);
    static readonly DateTime NextDay = Hour10.Date.AddDays(1);

    [Test]
    public async Task Round_trips_a_small_uncompressed_body()
    {
        var store = await CreateContainer();
        var bodyId = Guid.NewGuid().ToString();
        var body = Encoding.UTF8.GetBytes("hello world");

        await store.WriteBody(bodyId, Hour10, body, "text/plain");

        var result = await store.ReadBody(bodyId, Hour10);

        Assert.That(result, Is.Not.Null);
        await using (result.Stream)
        {
            Assert.That(ReadAll(result.Stream), Is.EqualTo(body));
        }

        Assert.Multiple(() =>
        {
            Assert.That(result.ContentType, Is.EqualTo("text/plain"));
            Assert.That(result.BodySize, Is.EqualTo(body.Length));
        });
    }

    [Test]
    public async Task Round_trips_a_large_body_over_the_compression_threshold()
    {
        var store = await CreateContainer();
        var bodyId = Guid.NewGuid().ToString();
        var body = Encoding.UTF8.GetBytes(new string('a', 100_000));

        await store.WriteBody(bodyId, Hour10, body, "application/json");

        var result = await store.ReadBody(bodyId, Hour10);

        Assert.That(result, Is.Not.Null);
        await using (result.Stream)
        {
            Assert.That(ReadAll(result.Stream), Is.EqualTo(body));
        }

        Assert.That(result.BodySize, Is.EqualTo(body.Length));
    }

    [Test]
    public async Task Returns_null_for_a_missing_body()
    {
        var store = await CreateContainer();
        Assert.That(await store.ReadBody(Guid.NewGuid().ToString(), Hour10), Is.Null);
    }

    [Test]
    public async Task Read_for_another_hour_finds_nothing()
    {
        var store = await CreateContainer();
        await store.WriteBody("a", Hour10, Encoding.UTF8.GetBytes("payload"), "text/plain");

        Assert.That(await store.ReadBody("a", Hour11), Is.Null);
    }

    [Test]
    public async Task Delete_for_hour_removes_only_that_hour()
    {
        var store = await CreateContainer();
        var body = Encoding.UTF8.GetBytes("payload");
        await store.WriteBody("a", Hour10, body, "text/plain");
        await store.WriteBody("b", Hour10, body, "text/plain");
        await store.WriteBody("a", Hour11, body, "text/plain");

        await store.DeleteBodiesForHour(Hour10);

        Assert.Multiple(async () =>
        {
            Assert.That(await store.ReadBody("a", Hour10), Is.Null);
            Assert.That(await store.ReadBody("b", Hour10), Is.Null);
            Assert.That(await store.ReadBody("a", Hour11), Is.Not.Null);
        });
    }

    [Test]
    public async Task Delete_for_hour_removes_more_bodies_than_fit_in_one_batch()
    {
        var store = await CreateContainer();
        var body = Encoding.UTF8.GetBytes("payload");
        var ids = Enumerable.Range(0, 300).Select(_ => Guid.NewGuid().ToString()).ToArray();
        await Task.WhenAll(ids.Select(id => store.WriteBody(id, Hour10, body, "text/plain")));
        await store.WriteBody("other", Hour11, body, "text/plain");

        await store.DeleteBodiesForHour(Hour10);

        Assert.Multiple(async () =>
        {
            foreach (var id in ids)
            {
                Assert.That(await store.ReadBody(id, Hour10), Is.Null, id);
            }

            Assert.That(await store.ReadBody("other", Hour11), Is.Not.Null);
        });
    }

    [Test]
    public async Task Delete_for_day_removes_every_hour_of_that_day_only()
    {
        var store = await CreateContainer();
        var body = Encoding.UTF8.GetBytes("payload");
        await store.WriteBody("a", Hour10.Date, body, "text/plain");
        await store.WriteBody("b", Hour10.Date.AddHours(23), body, "text/plain");
        await store.WriteBody("c", NextDay, body, "text/plain");
        await store.WriteBody("d", Hour10.Date.AddHours(-1), body, "text/plain");

        await store.DeleteBodiesForDay(Hour10);

        Assert.Multiple(async () =>
        {
            Assert.That(await store.ReadBody("a", Hour10.Date), Is.Null);
            Assert.That(await store.ReadBody("b", Hour10.Date.AddHours(23)), Is.Null);
            Assert.That(await store.ReadBody("c", NextDay), Is.Not.Null);
            Assert.That(await store.ReadBody("d", Hour10.Date.AddHours(-1)), Is.Not.Null);
        });
    }

    [Test]
    public async Task Delete_succeeds_when_nothing_is_stored()
    {
        var store = await CreateContainer();

        Assert.DoesNotThrowAsync(() => store.DeleteBodiesForHour(Hour10));
        Assert.DoesNotThrowAsync(() => store.DeleteBodiesForDay(Hour10));
    }

    [Test]
    public async Task Rewriting_an_existing_body_keeps_the_first_write()
    {
        var store = await CreateContainer();
        var bodyId = Guid.NewGuid().ToString();
        var original = Encoding.UTF8.GetBytes("original");

        await store.WriteBody(bodyId, Hour10, original, "text/plain");
        await store.WriteBody(bodyId, Hour10, Encoding.UTF8.GetBytes("different"), "text/plain");

        var result = await store.ReadBody(bodyId, Hour10);

        Assert.That(result, Is.Not.Null);
        await using (result.Stream)
        {
            Assert.That(ReadAll(result.Stream), Is.EqualTo(original), "bodies are immutable, so the first write wins");
        }
    }

    static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
