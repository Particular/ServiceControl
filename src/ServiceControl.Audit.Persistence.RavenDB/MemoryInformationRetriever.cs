namespace ServiceControl.Audit.Persistence.RavenDB;

using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class MemoryInformationRetriever(string serverUrl)
{
    readonly HttpClient client = new() { BaseAddress = new Uri(serverUrl) };

    record ResponseDto
    {
        public MemoryInformation MemoryInformation { get; set; }
    }

    record MemoryInformation
    {
        public bool IsHighDirty { get; set; }
        public string DirtyMemory { get; set; }
    }

    public async Task<(bool IsHighDirty, int DirtyMemory)> GetMemoryInformation(CancellationToken cancellationToken = default)
    {
        var httpResponse = await client.GetAsync("/admin/debug/memory/stats", cancellationToken);
        var responseDto = JsonSerializer.Deserialize<ResponseDto>(await httpResponse.Content.ReadAsStringAsync(cancellationToken));

        return (responseDto.MemoryInformation.IsHighDirty, int.Parse(responseDto.MemoryInformation.DirtyMemory.Split(' ').First()));
    }
}