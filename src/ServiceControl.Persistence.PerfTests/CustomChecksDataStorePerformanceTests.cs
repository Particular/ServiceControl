namespace ServiceControl.Persistence.PerfTests
{
    using System;
    using System.Threading.Tasks;
    using ServiceControl.Contracts.CustomChecks;
    using ServiceControl.Contracts.Operations;
    using NBench;

    class CustomCheckPerfTests : PerformanceTest
    {
        const double AcceptableInsertThroughput = 100.0D;

        [PerfBenchmark(Description = "CustomCheck status update check a minimal throughput can be achieved",
            NumberOfIterations = 1000,
            RunMode = RunMode.Iterations,
            TestMode = TestMode.Test,
            SkipWarmups = true)]
        [ElapsedTimeAssertion(MaxTimeMilliseconds = 50)]
        [CounterThroughputAssertion("InsertionCounter", MustBe.GreaterThanOrEqualTo, AcceptableInsertThroughput)]
        public void CustomCheck_Insert_Must_Be_Acceptable()
        {
            var run = Guid.NewGuid();
            var _ = CustomCheckDataStore.UpdateCustomCheckStatus(new CustomCheckDetail
            {
                Category = "category-" + run,
                HasFailed = false,
                CustomCheckId = run.ToString(),
                OriginatingEndpoint = new EndpointDetails
                {
                    Host = run.ToString(),
                    HostId = run,
                    Name = "test-run-" + run
                }
            }).GetAwaiter().GetResult();

            counter.Increment();
        }

        protected override async Task SetupTest(BenchmarkContext context)
        {
            await base.SetupTest(context).ConfigureAwait(false);
            counter = context.GetCounter("InsertionCounter");
        }

        Counter counter;
    }
}