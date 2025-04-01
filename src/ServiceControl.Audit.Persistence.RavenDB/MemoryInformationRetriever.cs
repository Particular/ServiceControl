namespace ServiceControl.Audit.Persistence.RavenDB;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class MemoryInformationRetriever(DatabaseConfiguration databaseConfiguration)
{
    // Connection string is composed of the server URL. The ?? operator is needed because ServerUrl
    // is populated when running embedded and connection string when running in external mode.
    // However, the tricky part is that when tests are run they behave like if it was external mode.
    // Only one of ConnectionString and ServerUrl will be non-null, so we'll check ConnectionString first
    // to be consistent with the error instance implementation, where ServerUrl always has a value.
    readonly HttpClient client = new() { BaseAddress = new Uri(databaseConfiguration.ServerConfiguration.ConnectionString ?? databaseConfiguration.ServerConfiguration.ServerUrl) };

    record ResponseDto
    {
        public MemoryInformation MemoryInformation { get; set; }
    }

    record MemoryInformation
    {
        public bool IsHighDirty { get; set; }
        public string DirtyMemory { get; set; }
    }

    public async Task<(bool IsHighDirty, string DirtyMemory)> GetMemoryInformation(CancellationToken cancellationToken = default)
    {
        var httpResponse = await client.GetAsync("/admin/debug/memory/stats?includeThreads=false&includeMappings=false", cancellationToken);
        var responseDto = JsonSerializer.Deserialize<ResponseDto>(await httpResponse.Content.ReadAsStringAsync(cancellationToken));

        return (responseDto.MemoryInformation.IsHighDirty, responseDto.MemoryInformation.DirtyMemory);
    }
}