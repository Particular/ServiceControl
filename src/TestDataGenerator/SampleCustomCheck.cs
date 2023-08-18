namespace TestDataGenerator
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.CustomChecks;

    public class SampleCustomCheck : CustomCheck
    {
        EndpointContext context;

        public SampleCustomCheck(EndpointContext context)
            : base("SampleCustomCheck", "SomeCategory", TimeSpan.FromSeconds(5))
        {
            this.context = context;
        }

        public override Task<CheckResult> PerformCheck()
        {
            if (context.FailCustomCheck)
            {
                return Task.FromResult(CheckResult.Failed("Configured to fail on " + context.Name));
            }

            return Task.FromResult(CheckResult.Pass);
        }
    }
}
