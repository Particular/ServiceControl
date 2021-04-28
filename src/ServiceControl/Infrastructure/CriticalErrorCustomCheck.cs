namespace ServiceControl.Operations
{
    using System;
    using System.Threading.Tasks;
    using CustomChecks;
    using NServiceBus;
    using NServiceBus.CustomChecks;
    using CustomCheck = NServiceBus.CustomChecks.CustomCheck;

    class CriticalErrorCustomCheck : CustomCheck
    {
        static volatile string recentFailure;

        public CriticalErrorCustomCheck()
            : base("ServiceControl Primary Instance", CustomCheckCategories.ServiceControlHealth, TimeSpan.FromSeconds(60))
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