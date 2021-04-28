namespace ServiceControl.Operations
{
    using System;
    using System.Threading.Tasks;
    using CustomChecks;
    using NServiceBus.CustomChecks;
    using CustomCheck = NServiceBus.CustomChecks.CustomCheck;

    class ErrorIngestionCustomCheck : CustomCheck
    {
        public override Task<CheckResult> PerformCheck()
        {
            var failure = criticalErrorHolder.GetLastFailure();
            return failure == null
                ? successResult
                : Task.FromResult(CheckResult.Failed(failure));
        }

        public ErrorIngestionCustomCheck(State criticalErrorHolder)
            : base("Error Message Ingestion Process", CustomChecksCategories.ServiceControlHealth, TimeSpan.FromSeconds(5))
        {
            this.criticalErrorHolder = criticalErrorHolder;
        }

        readonly State criticalErrorHolder;
        static Task<CheckResult> successResult = Task.FromResult(CheckResult.Pass);

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