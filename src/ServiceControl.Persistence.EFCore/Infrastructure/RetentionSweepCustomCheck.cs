namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System.Threading;
using System.Threading.Tasks;
using NServiceBus.CustomChecks;

// The standard custom-check state-transition pipeline deduplicates repeated results.
class RetentionSweepCustomCheck(RetentionSweepCustomCheck.State state)
    : CustomCheck("ServiceControl Retention", "ServiceControl Health", TimeSpan.FromMinutes(1))
{
    public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var failureSummary = state.GetFailureSummary();

        return Task.FromResult(failureSummary is null
            ? CheckResult.Pass
            : CheckResult.Failed($"Retention processing has failures. Last failure per entity: {failureSummary}. See https://docs.particular.net/servicecontrol/troubleshooting for guidance on resolving the issue."));
    }

    public class State
    {
        readonly Dictionary<RetentionEntity, string> failures = [];
        int consecutiveFailedSweeps;

        public void Clear(RetentionEntity entity)
        {
            lock (failures)
            {
                failures.Remove(entity);
            }
        }

        public void ReportError(RetentionEntity entity, string reason)
        {
            lock (failures)
            {
                failures[entity] = reason;
            }
        }

        public void SweepComplete()
        {
            lock (failures)
            {
                consecutiveFailedSweeps = failures.Count == 0 ? 0 : consecutiveFailedSweeps + 1;
            }
        }

        string? GetFailureSummary()
        {
            lock (failures)
            {
                return consecutiveFailedSweeps < 3
                    ? null
                    : string.Join("; ", failures.Select(failure => $"{failure.Key}: {failure.Value}"));
            }
        }
    }
}
