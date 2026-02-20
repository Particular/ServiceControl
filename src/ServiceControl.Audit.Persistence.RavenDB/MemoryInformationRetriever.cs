namespace ServiceControl.Audit.Persistence.RavenDB;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class MemoryInformationRetriever(IRavenDocumentStoreProvider documentStoreProvider)
{
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
        var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
        var client = documentStore.GetRequestExecutor().HttpClient;
        var requestUrl = documentStore.Urls[0].TrimEnd('/') + "/admin/debug/memory/stats?includeThreads=false&includeMappings=false";
        var httpResponse = await client.GetAsync(requestUrl, cancellationToken);
        var responseDto = JsonSerializer.Deserialize<ResponseDto>(await httpResponse.Content.ReadAsStringAsync(cancellationToken));

        return responseDto.MemoryInformation is null
            ? default
            : (responseDto.MemoryInformation.IsHighDirty, responseDto.MemoryInformation.DirtyMemory);
    }
}