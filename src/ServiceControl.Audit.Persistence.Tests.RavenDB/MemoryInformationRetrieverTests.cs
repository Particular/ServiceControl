namespace ServiceControl.Audit.Persistence.Tests;

using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Persistence.RavenDB;

[TestFixture]
class MemoryInformationRetrieverTests : PersistenceTestFixture
{
    // MemoryInformationRetriever deserializes the response into its own types. A renamed property leaves the default
    // value in place and the dirty memory check reports no problem, so the names are asserted on the raw response.
    [Test]
    public async Task Memory_stats_should_contain_the_properties_read_by_the_dirty_memory_check()
    {
        var requestUrl = configuration.DocumentStore.Urls.First().TrimEnd('/') + "/admin/debug/memory/stats?includeThreads=false&includeMappings=false";

        using var http = new HttpClient();
        var json = await http.GetStringAsync(requestUrl, TestTimeoutCancellationToken);

        using var document = JsonDocument.Parse(json);
        Assert.That(document.RootElement.TryGetProperty("MemoryInformation", out var memoryInformation), Is.True, "MemoryInformation is missing from the response");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(memoryInformation.TryGetProperty("IsHighDirty", out var isHighDirty), Is.True, "MemoryInformation.IsHighDirty is missing from the response");
            Assert.That(isHighDirty.ValueKind, Is.AnyOf(JsonValueKind.True, JsonValueKind.False));
            Assert.That(memoryInformation.TryGetProperty("DirtyMemory", out var dirtyMemory), Is.True, "MemoryInformation.DirtyMemory is missing from the response");
            Assert.That(dirtyMemory.ValueKind, Is.EqualTo(JsonValueKind.String));
        }
    }

    [Test]
    public async Task Should_retrieve_the_dirty_memory_of_the_server()
    {
        var retriever = ServiceProvider.GetRequiredService<MemoryInformationRetriever>();

        var (_, dirtyMemory) = await retriever.GetMemoryInformation(TestTimeoutCancellationToken);

        Assert.That(dirtyMemory, Is.Not.Null.And.Not.Empty);
    }
}
