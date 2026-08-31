namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Implementation.BodyStorage;
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

    public async Task<AzureBlobBodyStoragePersistence> CreateContainer()
    {
        var settings = new AzureBlobBodyStorageSettings
        {
            MinCompressionSize = 64,
            Authentication = new AzureBlobSharedKeyAuthentication { ConnectionString = azurite.GetConnectionString() },
            ContainerName = $"bodies-{Guid.NewGuid():n}"
        };

        await new AzureBlobBodyStorageInstaller(settings).Provision();
        return new AzureBlobBodyStoragePersistence(settings);
    }

    [Test]
    public async Task Round_trips_a_small_uncompressed_body()
    {
        var store = await CreateContainer();
        var bodyId = Guid.NewGuid().ToString();
        var body = Encoding.UTF8.GetBytes("hello world");

        await store.WriteBody(bodyId, body, "text/plain");

        var result = await store.ReadBody(bodyId);

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

        await store.WriteBody(bodyId, body, "application/json");

        var result = await store.ReadBody(bodyId);

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
        Assert.That(await store.ReadBody(Guid.NewGuid().ToString()), Is.Null);
    }

    [Test]
    public async Task Delete_removes_the_body()
    {
        var store = await CreateContainer();
        var bodyId = Guid.NewGuid().ToString();
        await store.WriteBody(bodyId, "payload"u8.ToArray(), "text/plain");

        await store.DeleteBodyIfExists(bodyId);

        Assert.That(await store.ReadBody(bodyId), Is.Null);
    }

    [Test]
    public async Task Delete_of_a_missing_body_does_not_throw()
    {
        var store = await CreateContainer();
        Assert.DoesNotThrowAsync(() => store.DeleteBodyIfExists(Guid.NewGuid().ToString()));
    }

    [Test]
    public async Task Rewriting_an_existing_body_keeps_the_first_write()
    {
        var store = await CreateContainer();
        var bodyId = Guid.NewGuid().ToString();
        var original = "original"u8.ToArray();

        await store.WriteBody(bodyId, original, "text/plain");
        await store.WriteBody(bodyId, "different"u8.ToArray(), "text/plain");

        var result = await store.ReadBody(bodyId);

        Assert.That(result, Is.Not.Null);
        await using (result.Stream)
        {
            var data = ReadAll(result.Stream);
            Assert.That(data, Is.EqualTo(original), "bodies are immutable, so the first write wins");
        }
    }

    static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
