﻿namespace ServiceControl.Monitoring.UnitTests.MonitoredEndpoints
{
    using System;
    using System.Collections.Generic;
    using Http.Diagrams;
    using Monitoring.Infrastructure;
    using NUnit.Framework;

    [TestFixture]
    public class QueueLengthAggregationTests
    {
        [Test]
        public void Point_value_is_sum_of_averages_per_input_queue()
        {
            var intervals = new List<IntervalsStore<EndpointInputQueue>.IntervalsBreakdown>
            {
                new IntervalsStore<EndpointInputQueue>.IntervalsBreakdown
                {
                    Id = new EndpointInputQueue(endpointName: "", inputQueue: "queue-1"),
                    Intervals = new[]
                    {
                        new IntervalsStore<EndpointInputQueue>.TimeInterval {IntervalStart = now, TotalValue = 3, TotalMeasurements = 4},
                        new IntervalsStore<EndpointInputQueue>.TimeInterval {IntervalStart = now.AddSeconds(1), TotalValue = 2, TotalMeasurements = 3}
                    }
                },
                new IntervalsStore<EndpointInputQueue>.IntervalsBreakdown
                {
                    Id = new EndpointInputQueue(endpointName: "", inputQueue: "queue-2"),
                    Intervals = new[]
                    {
                        new IntervalsStore<EndpointInputQueue>.TimeInterval {IntervalStart = now, TotalValue = 5, TotalMeasurements = 6}
                    }
                }
            };

            var values = Aggregator.ToRoundedSumOfBreakdownAverages(intervals, HistoryPeriod.FromMinutes(5));

            Assert.Multiple(() =>
            {
                Assert.That(values.Points[0], Is.EqualTo(Math.Round((3d / 4d) + (5d / 6d))));
                Assert.That(values.Points[1], Is.EqualTo(Math.Round(2d / 3d)));
            });
        }

        [Test]
        public void Total_average_is_sum_of_averages_per_input_queue()
        {
            var intervals = new List<IntervalsStore<EndpointInputQueue>.IntervalsBreakdown>
            {
                new IntervalsStore<EndpointInputQueue>.IntervalsBreakdown
                {
                    Id = new EndpointInputQueue(endpointName: "", inputQueue: "queue-1"),
                    TotalValue = 3,
                    TotalMeasurements = 1,
                    Intervals = EmptyIntervals
                },
                new IntervalsStore<EndpointInputQueue>.IntervalsBreakdown
                {
                    Id = new EndpointInputQueue(endpointName: "", inputQueue: "queue-2"),
                    TotalValue = 41,
                    TotalMeasurements = 5,
                    Intervals = EmptyIntervals
                }
            };

            var values = Aggregator.ToRoundedSumOfBreakdownAverages(intervals, HistoryPeriod.FromMinutes(5));

            Assert.That(values.Average, Is.EqualTo(Math.Round((3d / 1d) + (41d / 5d))));
        }

        [Test]
        public void Intervals_are_merged_by_interval_start()
        {
            var intervals = new List<IntervalsStore<EndpointInputQueue>.IntervalsBreakdown>
            {
                new IntervalsStore<EndpointInputQueue>.IntervalsBreakdown
                {
                    Id = new EndpointInputQueue(endpointName: "", inputQueue: "queue-1"),
                    Intervals = new[]
                    {
                        new IntervalsStore<EndpointInputQueue>.TimeInterval {IntervalStart = now},
                        new IntervalsStore<EndpointInputQueue>.TimeInterval {IntervalStart = now.AddSeconds(2)}
                    }
                },
                new IntervalsStore<EndpointInputQueue>.IntervalsBreakdown
                {
                    Id = new EndpointInputQueue(endpointName: "", inputQueue: "queue-2"),
                    Intervals = new[]
                    {
                        new IntervalsStore<EndpointInputQueue>.TimeInterval {IntervalStart = now},
                        new IntervalsStore<EndpointInputQueue>.TimeInterval {IntervalStart = now.AddSeconds(2)},
                        new IntervalsStore<EndpointInputQueue>.TimeInterval {IntervalStart = now.AddSeconds(3)}
                    }
                }
            };

            var values = Aggregator.ToRoundedSumOfBreakdownAverages(intervals, HistoryPeriod.FromMinutes(5));

            Assert.That(values.Points, Has.Length.EqualTo(3));
        }

        DateTime now = DateTime.UtcNow;
        static readonly IntervalsStore<EndpointInputQueue>.TimeInterval[] EmptyIntervals = new IntervalsStore<EndpointInputQueue>.TimeInterval[0];
    }
}