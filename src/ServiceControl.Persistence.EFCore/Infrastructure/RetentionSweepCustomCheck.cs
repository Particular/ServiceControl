namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.CustomChecks;

// Fails after any retention pass fails and recovers after the next fully successful sweep.
// The standard custom-check state-transition pipeline deduplicates repeated results.
class RetentionSweepCustomCheck(RetentionSweeper sweeper)
    : CustomCheck("ServiceControl Retention", "ServiceControl Health", TimeSpan.FromMinutes(1))
{
    public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var activeFailures = sweeper.GetActiveFailures();

        if (activeFailures.Length == 0)
        {
            return Task.FromResult(CheckResult.Pass);
        }

        var summary = string.Join("; ", activeFailures.Select(f => $"{f.Entity}: {f.Reason}"));
        return Task.FromResult(CheckResult.Failed(
            $"Retention processing has failures. Last failure per entity: {summary}"));
    }
}