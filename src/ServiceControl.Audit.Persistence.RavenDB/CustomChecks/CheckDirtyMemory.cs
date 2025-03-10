namespace ServiceControl.Audit.Persistence.RavenDB.CustomChecks;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.CustomChecks;

class CheckDirtyMemory(IRavenDocumentStoreProvider documentStoreProvider) : CustomCheck("ServiceControl.Audit database", "Dirty memory trends", TimeSpan.FromMinutes(5))
{
    readonly List<int> lastDirtyMemoryReads = [];
    public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var retriever = await GetMemoryRetriever(cancellationToken);
        var memoryInfo = await retriever.GetMemoryInformation(cancellationToken);

        if (memoryInfo.IsHighDirty)
        {
            //log warning
            return CheckResult.Failed("There is a high level of dirty memory. Check the ServiceControl " +
                                      "troubleshooting guide for guidance on how to mitigate the issue.");
        }

        lastDirtyMemoryReads.Add(memoryInfo.DirtyMemory);
        if (lastDirtyMemoryReads.Count > 20)
        {
            //cap the list at 20
            lastDirtyMemoryReads.RemoveAt(lastDirtyMemoryReads.Count - 1);
        }

        // evaluate the trends
        // if the amount of dirty memory is constantly growing log a warning and fail the check

        return CheckResult.Pass;
    }

    MemoryInformationRetriever _retriever;
    async Task<MemoryInformationRetriever> GetMemoryRetriever(CancellationToken cancellationToken = default)
    {
        if (_retriever == null)
        {
            var documentStore = await documentStoreProvider.GetDocumentStore(cancellationToken);
            var serverUrl = documentStore.Urls[0]; //TODO is there a better way to get the RavenDB server URL?
            _retriever = new MemoryInformationRetriever(serverUrl);
        }
        return _retriever;
    }
}