namespace ServiceControl.Monitoring.UnitTests.MonitoredEndpoints
{
    using System;
    using System.Collections.Generic;
    using Http.Diagrams;
    using Monitoring.Infrastructure;
    using NUnit.Framework;

    [TestFixture]
    public class AggregatorTests
    {
        [Test]
        public void Timings_average_is_sum_of_total_values_by_total_measurements()
        {
            var intervals = new List<IntervalsStore<BreakdownId>.IntervalsBreakdown>
            {
                new IntervalsStore<BreakdownId>.IntervalsBreakdown
                {
                    Id = new BreakdownId {Id = 0},
                    TotalMeasurements = 2,
                    TotalValue = 2,
                    Intervals = EmptyIntervals
                },
                new IntervalsStore<BreakdownId>.IntervalsBreakdown
                {
                    Id = new BreakdownId {Id = 0},
                    TotalMeasurements = 4,
                    TotalValue = 1,
                    Intervals = EmptyIntervals
                }
            };

            var values = Aggregator.ToAverages(intervals, HistoryPeriod.FromMinutes(5));

            Assert.That(values.Average, Is.EqualTo(0.5d));
        }

        [Test]
        public void Timings_intervals_are_merged_by_interval_start()
        {
            var intervals = new List<IntervalsStore<BreakdownId>.IntervalsBreakdown>
            {
                new IntervalsStore<BreakdownId>.IntervalsBreakdown
                {
                    Id = new BreakdownId {Id = 0},
                    Intervals = new[]
                    {
                        new IntervalsStore<BreakdownId>.TimeInterval {IntervalStart = now, TotalMeasurements = 1, TotalValue = 1}
                    }
                },
                new IntervalsStore<BreakdownId>.IntervalsBreakdown
                {
                    Id = new BreakdownId {Id = 0},
                    Intervals = new[]
                    {
                        new IntervalsStore<BreakdownId>.TimeInterval {IntervalStart = now, TotalMeasurements = 2, TotalValue = 2}
                    }
                }
            };

            var values = Aggregator.ToAverages(intervals, HistoryPeriod.FromMinutes(5));

            Assert.That(values.Points, Has.Length.EqualTo(1));
            Assert.That(values.Points[0], Is.EqualTo(1.0d));
        }

        [Test]
        public void Total_measurements_per_second_are_merged_by_interval_start()
        {
            const long ridiculouslyBigLong1 = 374859734593849583;
            const long ridiculouslyBigLong2 = 898394895890348954;

            var intervals = new List<IntervalsStore<BreakdownId>.IntervalsBreakdown>
            {
                new IntervalsStore<BreakdownId>.IntervalsBreakdown
                {
                    Id = new BreakdownId {Id = 0},
                    Intervals = new[]
                    {
                        new IntervalsStore<BreakdownId>.TimeInterval {IntervalStart = now, TotalValue = ridiculouslyBigLong1, TotalMeasurements = 4},
                        new IntervalsStore<BreakdownId>.TimeInterval {IntervalStart = now.AddSeconds(2), TotalValue = ridiculouslyBigLong2, TotalMeasurements = 5}
                    },
                    TotalMeasurements = 4 + 5
                },
                new IntervalsStore<BreakdownId>.IntervalsBreakdown
                {
                    Id = new BreakdownId {Id = 0},
                    Intervals = new[]
                    {
                        new IntervalsStore<BreakdownId>.TimeInterval {IntervalStart = now, TotalValue = ridiculouslyBigLong1, TotalMeasurements = 6},
                        new IntervalsStore<BreakdownId>.TimeInterval {IntervalStart = now.AddSeconds(2), TotalValue = ridiculouslyBigLong2, TotalMeasurements = 7}
                    },
                    TotalMeasurements = 6 + 7
                }
            };

            var period = HistoryPeriod.FromMinutes(5);
            var seconds = period.IntervalSize.TotalSeconds;
            var values = Aggregator.ToTotalMeasurementsPerSecond(intervals, period);

            Assert.Multiple(() =>
            {
                Assert.That(values.Average, Is.EqualTo((4d + 5d + 6d + 7d) / 2 / seconds));
                Assert.That(values.Points, Has.Length.EqualTo(2));
            });
            Assert.Multiple(() =>
            {
                Assert.That(values.Points[0], Is.EqualTo((4d + 6d) / seconds));
                Assert.That(values.Points[1], Is.EqualTo((5d + 7d) / seconds));
            });
        }

        [Test]
        public void Total_measurements_per_second_are_sum_of_total_measurements_by_number_of_unique_intervals_by_seconds()
        {
            var intervals = new List<IntervalsStore<BreakdownId>.IntervalsBreakdown>
            {
                new IntervalsStore<BreakdownId>.IntervalsBreakdown
                {
                    Id = new BreakdownId {Id = 0},
                    Intervals = new[]
                    {
                        new IntervalsStore<BreakdownId>.TimeInterval {IntervalStart = now, TotalMeasurements = 7},
                        new IntervalsStore<BreakdownId>.TimeInterval {IntervalStart = now.AddSeconds(1)}
                    }
                },
                new IntervalsStore<BreakdownId>.IntervalsBreakdown
                {
                    Id = new BreakdownId {Id = 0},
                    Intervals = new[]
                    {
                        new IntervalsStore<BreakdownId>.TimeInterval {IntervalStart = now},
                        new IntervalsStore<BreakdownId>.TimeInterval {IntervalStart = now.AddSeconds(1), TotalMeasurements = 9},
                        new IntervalsStore<BreakdownId>.TimeInterval {IntervalStart = now}
                    }
                }
            };

            var period = HistoryPeriod.FromMinutes(5);
            var seconds = period.IntervalSize.TotalSeconds;

            var values = Aggregator.ToTotalMeasurementsPerSecond(intervals, period);

            Assert.That(values.Average, Is.EqualTo((7d + 9d) / 2 / seconds));
        }

        DateTime now = DateTime.UtcNow;
        static readonly IntervalsStore<BreakdownId>.TimeInterval[] EmptyIntervals = new IntervalsStore<BreakdownId>.TimeInterval[0];

        class BreakdownId
        {
            public int Id { get; set; }

            protected bool Equals(BreakdownId other)
            {
                return Id == other.Id;
            }

            public override bool Equals(object obj)
            {
                if (obj is null)
                {
                    return false;
                }

                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                if (obj.GetType() != GetType())
                {
                    return false;
                }

                return Equals((BreakdownId)obj);
            }

            public override int GetHashCode()
            {
                return Id;
            }
        }
    }
}