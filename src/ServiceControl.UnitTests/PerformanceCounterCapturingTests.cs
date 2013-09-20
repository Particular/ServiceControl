namespace ServiceControl.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using EndpointPlugin.Infrastructure.PerformanceCounters;
    using NUnit.Framework;
    using ServiceControl.EndpointPlugin.Operations.PerformanceData;

    [TestFixture]
    public class PerformanceCounterCapturingTests
    {
        [Test]
        public void Should_be_able_to_continously_capture_counter_data()
        {
            var capturer = new PerformanceCounterCapturer();

            var counterKey = "ProcessorTime";
            var counterName = "% Processor Time";
            var counterCategory = "Processor Information";
            var instanceName = "_Total";

            capturer.Start();
            capturer.EnableCapturing(counterCategory, counterName, instanceName, counterKey);

            List<DataPoint> datapoints;
            do
            {
                datapoints = capturer.GetCollectedData(counterKey);
            } while (!datapoints.Any());

            capturer.Stop();

            var datapoint = datapoints.First();

            Assert.GreaterOrEqual(DateTime.UtcNow, datapoint.Time);
            Assert.Greater(datapoint.Value, 0.0);
        }
    }
}