namespace ServiceControl.Audit.Persistence.RavenDB;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class MemoryInformationRetriever(DatabaseConfiguration databaseConfiguration)
{
    readonly HttpClient client = new() { BaseAddress = new Uri(databaseConfiguration.ServerConfiguration.ServerUrl) };

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
        var httpResponse = await client.GetAsync("/admin/debug/memory/stats", cancellationToken);
        var responseDto = JsonSerializer.Deserialize<ResponseDto>(await httpResponse.Content.ReadAsStringAsync(cancellationToken));

        var values = responseDto.MemoryInformation.DirtyMemory.Split(' ');
        if (!string.Equals(values[1],"KBytes", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unexpected response. Was expecting memory details in KBytes, instead received: {responseDto.MemoryInformation.DirtyMemory}");
        }
        return (responseDto.MemoryInformation.IsHighDirty, int.Parse(values[0]));
    }
}