namespace ServiceControl.Operations
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;

    class ErrorIngestionCustomCheck : CustomCheck
    {
        readonly State criticalErrorHolder;

        public ErrorIngestionCustomCheck(State criticalErrorHolder) 
            : base("Failed message ingestion process", "ServiceControl", TimeSpan.FromSeconds(5))
        {
            this.criticalErrorHolder = criticalErrorHolder;
        }

        public override Task<CheckResult> PerformCheck()
        {
            var failure = criticalErrorHolder.GetLastFailure();
            if (failure == null)
            {
                return Task.FromResult(CheckResult.Pass);
            }

            return Task.FromResult(CheckResult.Failed(failure));
        }

        public class State
        {
            volatile string lastFailure;

            public void Clear() => lastFailure = null;
            public void ReportError(string failure) => lastFailure = failure;
            public string GetLastFailure()
            {
                return lastFailure;
            }
        }
    }
}