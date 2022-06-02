namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using CustomChecks.Internal;
    using NServiceBus;

    class CriticalErrorCustomCheck : CustomCheck
    {
        static volatile string recentFailure;

        public CriticalErrorCustomCheck()
            : base("ServiceControl Primary Instance", "Health", TimeSpan.FromSeconds(60))
        {
        }

        public override Task<CheckResult> PerformCheck(CancellationToken cancellationToken = default)
        {
            var lastFailure = recentFailure;
            if (lastFailure != null)
            {
                recentFailure = null;
                return Task.FromResult(CheckResult.Failed(lastFailure));
            }

            return Task.FromResult(CheckResult.Pass);
        }

        public static Task OnCriticalError(ICriticalErrorContext criticalErrorContext)
        {
            recentFailure = criticalErrorContext.Error;
            return Task.CompletedTask;
        }
    }
}