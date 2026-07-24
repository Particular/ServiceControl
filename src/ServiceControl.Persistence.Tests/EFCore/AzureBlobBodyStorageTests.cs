namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Implementation;
using Testcontainers.Azurite;

[TestFixture]
[Platform(Exclude = "Win", Reason = "Azurite has no Windows container image")]
class AzureBlobBodyStorageTests
{
    AzuriteContainer azurite;
    AzureBlobBodyStoragePersistence store;

    [OneTimeSetUp]
    public async Task StartAzurite()
    {
        azurite = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest").Build();
        await azurite.StartAsync();
    }

    [OneTimeTearDown]
    public async Task StopAzurite()
    {
        if (azurite != null)
        {
            await azurite.DisposeAsync();
        }
    }

    [SetUp]
    public async Task CreateContainer()
    {
        var settings = new TestSettings
        {
            ConnectionString = "not-used",
            BodyStorageType = BodyStorageType.AzureBlob,
            AzureBlobConnectionString = azurite.GetConnectionString(),
            AzureBlobContainerName = $"bodies-{Guid.NewGuid():n}",
            MinBodySizeForCompression = 64
        };

        await new AzureBlobBodyStorageInstaller(settings).Provision();
        store = new AzureBlobBodyStoragePersistence(settings);
    }

    [Test]
    public async Task Round_trips_a_small_uncompressed_body()
    {
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

    [Test]
    public async Task Round_trips_a_large_body_over_the_compression_threshold()
    {
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

    [Test]
    public async Task Returns_null_for_a_missing_body() =>
        Assert.That(await store.ReadBody(Guid.NewGuid().ToString()), Is.Null);

    [Test]
    public async Task Delete_removes_the_body()
    {
        var bodyId = Guid.NewGuid().ToString();
        await store.WriteBody(bodyId, Encoding.UTF8.GetBytes("payload"), "text/plain");

        await store.DeleteBody(bodyId);

        Assert.That(await store.ReadBody(bodyId), Is.Null);
    }

    [Test]
    public void Delete_of_a_missing_body_does_not_throw() =>
        Assert.DoesNotThrowAsync(() => store.DeleteBody(Guid.NewGuid().ToString()));

    [Test]
    public async Task Rewriting_an_existing_body_keeps_the_first_write()
    {
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

    static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    sealed class TestSettings : EFPersisterSettings;
}
