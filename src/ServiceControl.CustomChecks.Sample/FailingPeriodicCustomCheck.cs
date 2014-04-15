namespace ServiceControl.CustomChecks.Sample
{
    using System;
    using Plugin.CustomChecks;

    class FailingPeriodicCustomCheck : PeriodicCheck
    {
        public FailingPeriodicCustomCheck(): base("FailingPeriodicCustomCheck", "PeriodicChecks", TimeSpan.FromSeconds(30))
        {
          
        }

        public override CheckResult PerformCheck()
        {
            return CheckResult.Failed("Periodic check failed");
        }
    }

    class SuccessfulPeriodicCustomCheck : PeriodicCheck
    {
        public SuccessfulPeriodicCustomCheck()
            : base("SuccessfulPeriodicCustomCheck", "PeriodicChecks", TimeSpan.FromSeconds(30))
        {

        }

        public override CheckResult PerformCheck()
        {
            return CheckResult.Pass;
        }
    }
}