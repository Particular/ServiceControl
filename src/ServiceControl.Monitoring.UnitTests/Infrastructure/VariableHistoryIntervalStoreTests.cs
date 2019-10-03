namespace ServiceControl.Monitoring.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using Monitoring.Infrastructure;
    using NUnit.Framework;

    [TestFixture]
    public class VariableHistoryIntervalStoreTests
    {
        [SetUp]
        public void SetUp()
        {
            now = DateTime.UtcNow;
        }

        [Test]
        public void Store_updates_all_supported_historical_periods()
        {
            var store = new VariableHistoryIntervalStore<int>();

            var entries = EntriesBuilder.Build(new Dictionary<DateTime, long>
            {
                {now, 5L}
            });

            store.Store(0, entries);

            foreach (var period in HistoryPeriod.All)
            {
                var reportDelay = TimeSpan.FromTicks(period.IntervalSize.Ticks * period.DelayedIntervals);

                var intervals = store.GetIntervals(period, now.Add(reportDelay));

                Assert.AreEqual(1, intervals.Length);
                Assert.AreEqual(5L, intervals[0].TotalValue);
                Assert.AreEqual(1L, intervals[0].TotalMeasurements);
            }
        }

        DateTime now;
    }
}