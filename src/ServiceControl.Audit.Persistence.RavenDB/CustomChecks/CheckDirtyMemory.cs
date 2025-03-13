namespace ServiceControl.Audit.Persistence.RavenDB.CustomChecks;

using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.CustomChecks;
using NServiceBus.Logging;

class CheckDirtyMemory(MemoryInformationRetriever memoryInformationRetriever) : CustomCheck("RavenDB dirty memory trends", "ServiceControl.Audit Health", TimeSpan.FromMinutes(5))
{
    public override async Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var (isHighDirty, dirtyMemoryKb) = await memoryInformationRetriever.GetMemoryInformation(cancellationToken);

        if (isHighDirty)
        {
            var message = $"There is a high level of RavenDB dirty memory ({dirtyMemoryKb}kb). Check the ServiceControl " +
                          "troubleshooting guide for guidance on how to mitigate the issue. " +
                          "Visit the https://docs.particular.net/servicecontrol/troubleshooting page for more information.";
            Log.Warn(message);
            return CheckResult.Failed(message);
        }

        return CheckResult.Pass;
    }

    static readonly ILog Log = LogManager.GetLogger<CheckDirtyMemory>();
}