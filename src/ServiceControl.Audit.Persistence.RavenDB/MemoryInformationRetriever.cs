namespace ServiceControl.Audit.Persistence.RavenDB;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class MemoryInformationRetriever(DatabaseConfiguration databaseConfiguration)
{
    // What does a connection string look like? Is it only a URI or could it contain other stuff?
    // The ?? operator is needed because ServerUrl is populated when running embedded and connection
    // string when running in external mode. However, the tricky part is that when tests are run they
    // behave like if it was external mode. If the connection string contain always only the server
    // URL, this code is safe, otherwise it need to be adjusted to extract the server URL.
    readonly HttpClient client = new() { BaseAddress = new Uri(databaseConfiguration.ServerConfiguration.ServerUrl ?? databaseConfiguration.ServerConfiguration.ConnectionString) };

    record ResponseDto
    {
        public MemoryInformation MemoryInformation { get; set; }
    }

    record MemoryInformation
    {
        public bool IsHighDirty { get; set; }
        public string DirtyMemory { get; set; }
    }

    public async Task<(bool IsHighDirty, int DirtyMemoryKb)> GetMemoryInformation(CancellationToken cancellationToken = default)
    {
        var httpResponse = await client.GetAsync("/admin/debug/memory/stats?includeThreads=false&includeMappings=false", cancellationToken);
        var responseDto = JsonSerializer.Deserialize<ResponseDto>(await httpResponse.Content.ReadAsStringAsync(cancellationToken));

        var values = responseDto.MemoryInformation.DirtyMemory.Split(' ');
        if (!string.Equals(values[1], "KBytes", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unexpected response. Was expecting memory details in KBytes, instead received: {responseDto.MemoryInformation.DirtyMemory}");
        }
        return (responseDto.MemoryInformation.IsHighDirty, int.Parse(values[0]));
    }
}