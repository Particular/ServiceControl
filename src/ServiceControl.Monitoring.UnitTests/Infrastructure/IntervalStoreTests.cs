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

            Assert.That(timings, Has.Length.EqualTo(1));
            Assert.That(timings[0].Intervals, Has.Length.EqualTo(33));

            // ordering of intervals
            var dateTimes = timings[0].Intervals.Select(i => i.IntervalStart).ToArray();
            var orderedDateTimes = dateTimes.OrderByDescending(d => d).ToArray();

            Assert.That(dateTimes, Is.EqualTo(orderedDateTimes).AsCollection);

            // length of intervals
            var intervalLength = dateTimes[0] - dateTimes[1];
            for (var i = 1; i < dateTimes.Length; i++)
            {
                var dateDiff = dateTimes[i - 1] - dateTimes[i];
                Assert.That(dateDiff, Is.EqualTo(intervalLength));
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

            Assert.Multiple(() =>
            {
                Assert.That(timings[0].TotalMeasurements, Is.EqualTo(1));
                Assert.That(timings[0].TotalValue, Is.EqualTo(2L));
            });
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

            Assert.That(timings[0].Intervals.All(i => i.TotalMeasurements == 0), Is.True);
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

            Assert.That(currentTimings[0].TotalMeasurements, Is.EqualTo(0));

            var futureTimings = store.GetIntervals(now.Add(store.IntervalSize).AddMinutes(6));

            Assert.That(futureTimings[0].TotalMeasurements, Is.EqualTo(1));
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

            Assert.Multiple(() =>
            {
                Assert.That(nonEmptyIntervals, Has.Length.EqualTo(3));
                Assert.That(timings[0].TotalMeasurements, Is.EqualTo(4));
            });
            Assert.That(nonEmptyIntervals.Select(i => i.TotalValue), Is.EqualTo(new double[] { 4, 1, 1 }).AsCollection);
            Assert.That(nonEmptyIntervals.Select(i => i.TotalMeasurements), Is.EqualTo(new double[] { 2, 1, 1 }).AsCollection);
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

            Assert.Multiple(() =>
            {
                Assert.That(intervalStarts[0], Is.GreaterThan(intervalStarts[1]));
                Assert.That(intervalStarts[1], Is.GreaterThan(intervalStarts[2]));
            });
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

            Assert.Multiple(() =>
            {
                Assert.That(timings[0].TotalMeasurements, Is.EqualTo(0));
                Assert.That(delayedTimings[0].TotalMeasurements, Is.EqualTo(3));
            });
        }

        IntervalsStore<int> AnyStore()
        {
            return new IntervalsStore<int>(TimeSpan.FromSeconds(5), 127, 0);
        }

        DateTime now;
    }
}