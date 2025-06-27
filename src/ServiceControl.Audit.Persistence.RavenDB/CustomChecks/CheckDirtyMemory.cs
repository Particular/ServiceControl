namespace ServiceControl.Audit.Persistence.RavenDB.CustomChecks;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NServiceBus.CustomChecks;

class CheckDirtyMemory(MemoryInformationRetriever memoryInformationRetriever, ILogger<CheckDirtyMemory> logger) : CustomCheck("RavenDB dirty memory", "ServiceControl.Audit Health", TimeSpan.FromMinutes(5))
{

    public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var (isHighDirty, dirtyMemory) = await memoryInformationRetriever.GetMemoryInformation(cancellationToken);

        logger.LogDebug("RavenDB dirty memory value: {DirtyMemory}", dirtyMemory);

        if (isHighDirty)
        {
            logger.LogWarning("There is a high level of RavenDB dirty memory ({DirtyMemory}). See https://docs.particular.net/servicecontrol/troubleshooting#ravendb-dirty-memory for guidance on how to mitigate the issue", dirtyMemory);
            return CheckResult.Failed($"There is a high level of RavenDB dirty memory ({dirtyMemory}). See https://docs.particular.net/servicecontrol/troubleshooting#ravendb-dirty-memory for guidance on how to mitigate the issue.");
        }

        return CheckResult.Pass;
    }
}