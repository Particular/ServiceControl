namespace ServiceControl.Audit.Persistence.EFCore.Implementation;

using NServiceBus.CustomChecks;

class AuditRetentionCustomCheck(AuditRetentionCustomCheck.State state)
    : CustomCheck("Audit retention", "ServiceControl.Audit Health", TimeSpan.FromHours(1))
{
    public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
    {
        var (consecutiveFailedSweeps, lastFailure) = state.Current;

        return Task.FromResult(consecutiveFailedSweeps < 3
            ? CheckResult.Pass
            : CheckResult.Failed($"The last {consecutiveFailedSweeps} audit retention sweeps failed. Last failure: {lastFailure}. See https://docs.particular.net/servicecontrol/troubleshooting for guidance on resolving the issue."));
    }

    internal class State
    {
        readonly object gate = new();
        int consecutiveFailedSweeps;
        string? lastFailure;

        public (int ConsecutiveFailedSweeps, string? LastFailure) Current
        {
            get
            {
                lock (gate)
                {
                    return (consecutiveFailedSweeps, lastFailure);
                }
            }
        }

        public void SweepSucceeded()
        {
            lock (gate)
            {
                consecutiveFailedSweeps = 0;
                lastFailure = null;
            }
        }

        public void SweepFailed(string reason)
        {
            lock (gate)
            {
                consecutiveFailedSweeps++;
                lastFailure = reason;
            }
        }
    }
}
