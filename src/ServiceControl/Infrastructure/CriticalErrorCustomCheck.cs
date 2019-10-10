namespace ServiceControl.Operations
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.CustomChecks;

    public class CriticalErrorCustomCheck : CustomCheck
    {
        static volatile string recentFailure;

        public CriticalErrorCustomCheck() 
            : base("Critical Error", "Health", TimeSpan.FromSeconds(60))
        {
        }

        public override Task<CheckResult> PerformCheck()
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