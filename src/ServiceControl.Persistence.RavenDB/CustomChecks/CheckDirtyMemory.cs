namespace ServiceControl.Persistence.RavenDB.CustomChecks;

using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.CustomChecks;
using NServiceBus.Logging;

class CheckDirtyMemory(MemoryInformationRetriever memoryInformationRetriever) : CustomCheck("RavenDB dirty memory", "ServiceControl Health", TimeSpan.FromMinutes(5))
{
    public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var (isHighDirty, dirtyMemory) = await memoryInformationRetriever.GetMemoryInformation(cancellationToken);

        Log.Debug($"RavenDB dirty memory value: {dirtyMemory}.");

        if (isHighDirty)
        {
            var message = $"There is a high level of RavenDB dirty memory ({dirtyMemory}). See https://docs.particular.net/servicecontrol/troubleshooting#ravendb-dirty-memory for guidance on how to mitigate the issue.";
            Log.Warn(message);
            return CheckResult.Failed(message);
        }

        return CheckResult.Pass;
    }

    static readonly ILog Log = LogManager.GetLogger<CheckDirtyMemory>();
}