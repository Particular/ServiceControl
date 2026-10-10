namespace ServiceControl.Persistence.EFCore.Infrastructure;

using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence.EFCore.Infrastructure.Metrics;
using NServiceBus.CustomChecks;

// The standard custom-check state-transition pipeline deduplicates repeated results.
class RetentionSweepCustomCheck(RetentionSweepCustomCheck.State state)
    : CustomCheck("ServiceControl Retention", "ServiceControl Health", TimeSpan.FromMinutes(1))
{
    public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var failures = state.GetFailures();
        var failureSummary = string.Join("; ", failures.Select(failure => $"{failure.Key}: {failure.Value}"));

        return Task.FromResult(state.ConsecutiveFailedSweeps < 3
            ? CheckResult.Pass
            : CheckResult.Failed($"Retention processing has failures. Last failure per entity: {failureSummary}. See https://docs.particular.net/servicecontrol/troubleshooting for guidance on resolving the issue."));
    }

    internal class State
    {
        readonly Dictionary<RetentionEntity, string> failures = [];

        public int ConsecutiveFailedSweeps { get; private set; }

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
                ConsecutiveFailedSweeps = failures.Count == 0 ? 0 : ConsecutiveFailedSweeps + 1;
            }
        }

        internal KeyValuePair<RetentionEntity, string>[] GetFailures()
        {
            lock (failures)
            {
                return failures.ToArray();
            }
        }
    }
}