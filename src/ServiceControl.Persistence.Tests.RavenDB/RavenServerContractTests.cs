namespace ServiceControl.Persistence.Tests.RavenDB;

using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.RavenDB;

// The persister reads these two endpoints into hand-written types. A field that the server renames
// deserializes to its default value instead of failing, so the checks that rely on them would pass silently.
[TestFixture]
class RavenServerContractTests : RavenPersistenceTestBase
{
    [Test]
    public async Task Memory_stats_contain_the_fields_the_dirty_memory_check_reads()
    {
        var json = await GetJson("/admin/debug/memory/stats?includeThreads=false&includeMappings=false");

        Assert.That(json.TryGetProperty("MemoryInformation", out var memoryInformation), Is.True);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(memoryInformation.TryGetProperty("IsHighDirty", out var isHighDirty) && isHighDirty.ValueKind is JsonValueKind.True or JsonValueKind.False, Is.True, "IsHighDirty is a boolean");
            Assert.That(memoryInformation.TryGetProperty("DirtyMemory", out var dirtyMemory) && dirtyMemory.ValueKind == JsonValueKind.String, Is.True, "DirtyMemory is a string");
        }
    }

    [Test]
    public async Task Memory_information_is_read_from_the_server()
    {
        var retriever = ServiceProvider.GetRequiredService<MemoryInformationRetriever>();

        var (_, dirtyMemory) = await retriever.GetMemoryInformation();

        Assert.That(dirtyMemory, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task License_status_contains_the_fields_the_license_check_reads()
    {
        var json = await GetJson("/license/status");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(json.TryGetProperty("Id", out var id) && id.ValueKind == JsonValueKind.String, Is.True, "Id is a string");
            Assert.That(json.TryGetProperty("LicensedTo", out var licensedTo) && licensedTo.ValueKind == JsonValueKind.String, Is.True, "LicensedTo is a string");
            Assert.That(json.TryGetProperty("Status", out var status) && status.ValueKind == JsonValueKind.String, Is.True, "Status is a string");
            Assert.That(json.TryGetProperty("Expired", out var expired) && expired.ValueKind is JsonValueKind.True or JsonValueKind.False, Is.True, "Expired is a boolean");
        }
    }

    [Test]
    public async Task License_is_valid()
    {
        var json = await GetJson("/license/status");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(json.GetProperty("Id").GetString(), Is.EqualTo("64c6a174-3f3a-4e7d-ac5d-b3eedd801460"));
            Assert.That(json.GetProperty("LicensedTo").GetString(), Is.EqualTo("NserviceBus Ltd"));
            Assert.That(json.GetProperty("Status").GetString(), Is.EqualTo("Commercial"));
            Assert.That(json.GetProperty("Expired").GetBoolean(), Is.False);
        }

        Assert.DoesNotThrowAsync(() => LicenseStatusCheck.WaitForLicenseOrThrow(DocumentStore));
    }

    async Task<JsonElement> GetJson(string path)
    {
        var client = DocumentStore.GetRequestExecutor().HttpClient;

        using var response = await client.GetAsync(DocumentStore.Urls[0].TrimEnd('/') + path);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
