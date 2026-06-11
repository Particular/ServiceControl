namespace ServiceControl.Audit.Auditing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;

    class AuditIngestionCustomCheck(AuditIngestionCustomCheck.State criticalErrorHolder) : CustomCheck("Audit Message Ingestion Process", "ServiceControl Health", TimeSpan.FromSeconds(5))
    {
        public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            var failure = criticalErrorHolder.GetLastFailure();
            return failure == null
                ? successResult
                : Task.FromResult(CheckResult.Failed(failure));
        }

        static readonly Task<CheckResult> successResult = Task.FromResult(CheckResult.Pass);

        public class State
        {
            volatile string lastFailure;

            public void Clear() => lastFailure = null;
            public void ReportError(string failure) => lastFailure = failure;
            public string GetLastFailure() => lastFailure;
        }
    }
}