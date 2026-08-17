namespace ServiceControl.Operations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.CustomChecks;

    class CriticalErrorCustomCheck : CustomCheck
    {
        static volatile string recentFailure;

        public CriticalErrorCustomCheck()
            : base("ServiceControl Primary Instance", "Health", TimeSpan.FromSeconds(60))
        {
        }

        internal CriticalErrorCustomCheck(TimeSpan interval)
            : base("ServiceControl Primary Instance", "Health", interval)
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

#pragma warning disable PS0018 // Matches the NServiceBus critical-error delegate, which takes no CancellationToken
        public static Task OnCriticalError(ICriticalErrorContext criticalErrorContext)
        {
            recentFailure = criticalErrorContext.Error;
            return Task.CompletedTask;
        }
#pragma warning restore PS0018
    }
}