namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System.Threading;
using System.Threading.Tasks;
using NServiceBus.CustomChecks;

interface IRetentionSweepHealth
{
    string? GetFailureSummary();
}

// Fails after any retention pass fails and recovers after the next fully successful sweep.
// The standard custom-check state-transition pipeline deduplicates repeated results.
class RetentionSweepCustomCheck(IRetentionSweepHealth retentionSweepHealth)
    : CustomCheck("ServiceControl Retention", "ServiceControl Health", TimeSpan.FromMinutes(1))
{
    public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var failureSummary = retentionSweepHealth.GetFailureSummary();

        return Task.FromResult(failureSummary is null
            ? CheckResult.Pass
            : CheckResult.Failed($"Retention processing has failures. Last failure per entity: {failureSummary}"));
    }
}