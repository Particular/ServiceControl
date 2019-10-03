namespace ServiceControl.Monitoring.UnitTests.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Monitoring.Infrastructure;
    using NUnit.Framework;

    [TestFixture]
    public class IntervalStoreTests
    {
        [SetUp]
        public void SetUp()
        {
            now = DateTime.UtcNow;
        }

        [Test]
        public void Returned_number_of_intervals_per_known_endpoint_equals_history_size()
        {
            var entries = EntriesBuilder.Build(new Dictionary<DateTime, long>
            {
                {now.AddSeconds(-9), 0L}
            });

            var store = new IntervalsStore<int>(TimeSpan.FromSeconds(10), 33, 0);

            store.Store(0, entries);

            var timings = store.GetIntervals(now.Add(store.IntervalSize));

            Assert.AreEqual(1, timings.Length);
            Assert.AreEqual(33, timings[0].Intervals.Length);

            // ordering of intervals
            var dateTimes = timings[0].Intervals.Select(i => i.IntervalStart).ToArray();
            var orderedDateTimes = dateTimes.OrderByDescending(d => d).ToArray();

            CollectionAssert.AreEqual(orderedDateTimes, dateTimes);

            // length of intervals
            var intervalLength = dateTimes[0] - dateTimes[1];
            for (var i = 1; i < dateTimes.Length; i++)
            {
                var dateDiff = dateTimes[i - 1] - dateTimes[i];
                Assert.AreEqual(intervalLength, dateDiff);
            }
        }

        [Test]
        public void With_single_measurement_global_stats_equals_interval_stats()
        {
            var entries = EntriesBuilder.Build(new Dictionary<DateTime, long>
            {
                {now.AddSeconds(-9), 2L}
            });

            var store = AnyStore();

            store.Store(0, entries);

            var timings = store.GetIntervals(now.Add(store.IntervalSize));

            Assert.AreEqual(1, timings[0].TotalMeasurements);
            Assert.AreEqual(2L, timings[0].TotalValue);
        }

        [Test]
        public void Intervals_older_than_history_size_are_discarded()
        {
            var intervalSize = TimeSpan.FromSeconds(10);
            var numberOfIntervals = 100;
            var historySize = TimeSpan.FromTicks(intervalSize.Ticks * numberOfIntervals);

            var entries = EntriesBuilder.Build(new Dictionary<DateTime, long>
            {
                {now.Subtract(historySize), 3L}
            });

            var store = new IntervalsStore<int>(intervalSize, numberOfIntervals, 0);

            store.Store(0, entries);

            var timings = store.GetIntervals(now.Add(store.IntervalSize));

            Assert.IsTrue(timings[0].Intervals.All(i => i.TotalMeasurements == 0));
        }

        [Test]
        public void Intervals_from_the_future_are_stored()
        {
            var entries = EntriesBuilder.Build(new Dictionary<DateTime, long>
            {
                {now.AddMinutes(5), 1L}
            });

            var store = AnyStore();

            store.Store(0, entries);

            var currentTimings = store.GetIntervals(now);

            Assert.IsTrue(currentTimings[0].TotalMeasurements == 0);

            var futureTimings = store.GetIntervals(now.Add(store.IntervalSize).AddMinutes(6));

            Assert.IsTrue(futureTimings[0].TotalMeasurements == 1);
        }

        [Test]
        public void Intervals_can_store_data_from_two_entry_arrays()
        {
            var firstEntries = EntriesBuilder.Build(new Dictionary<DateTime, long>
            {
                {now.AddSeconds(-15), 1L},
                {now, 1L}
            });

            var secondEntries = EntriesBuilder.Build(new Dictionary<DateTime, long>
            {
                {now.AddSeconds(-30), 1L},
                {now, 3L}
            });

            var store = AnyStore();

            store.Store(0, firstEntries);
            store.Store(0, secondEntries);

            var timings = store.GetIntervals(now.Add(store.IntervalSize));

            var nonEmptyIntervals = timings[0].Intervals.Where(i => i.TotalMeasurements > 0).ToArray();

            Assert.AreEqual(3, nonEmptyIntervals.Length);
            Assert.AreEqual(4, timings[0].TotalMeasurements);
            CollectionAssert.AreEqual(new double[] {4, 1, 1}, nonEmptyIntervals.Select(i => i.TotalValue));
            CollectionAssert.AreEqual(new double[] {2, 1, 1}, nonEmptyIntervals.Select(i => i.TotalMeasurements));
        }

        [Test]
        public void Intervals_are_returned_in_descending_order()
        {
            var entries = EntriesBuilder.Build(new Dictionary<DateTime, long>
            {
                {now.AddSeconds(-45), 1L},
                {now.AddSeconds(-30), 1L},
                {now, 1L}
            });

            var store = AnyStore();

            store.Store(0, entries);

            var timings = store.GetIntervals(now);
            var intervalStarts = timings[0].Intervals.Select(i => i.IntervalStart).ToArray();

            Assert.IsTrue(intervalStarts[0] > intervalStarts[1]);
            Assert.IsTrue(intervalStarts[1] > intervalStarts[2]);
        }

        [Test]
        public void Delayed_intervals_are_not_reported()
        {
            const int delayedIntervals = 5;
            var intervalSize = TimeSpan.FromSeconds(1);
            var delay = TimeSpan.FromTicks(delayedIntervals * intervalSize.Ticks);

            var entries = EntriesBuilder.Build(new Dictionary<DateTime, long>
            {
                {now.AddSeconds(-4), 1L},
                {now.AddSeconds(-3), 1L},
                {now, 1L}
            });

            var store = new IntervalsStore<int>(intervalSize, 10, delayedIntervals);

            store.Store(0, entries);

            var timings = store.GetIntervals(now);
            var delayedTimings = store.GetIntervals(now.Add(delay));

            Assert.AreEqual(0, timings[0].TotalMeasurements);
            Assert.AreEqual(3, delayedTimings[0].TotalMeasurements);
        }

        IntervalsStore<int> AnyStore()
        {
            return new IntervalsStore<int>(TimeSpan.FromSeconds(5), 127, 0);
        }

        DateTime now;
    }
}