﻿namespace ServiceControl.Monitoring.Http.Diagrams
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Infrastructure;
    using Microsoft.AspNetCore.Mvc;
    using QueueLength;
    using Timings;

    [ApiController]
    public class DiagramApiController(IEnumerable<IProvideBreakdown> breakdownProviders, EndpointRegistry endpointRegistry, EndpointInstanceActivityTracker activityTracker, MessageTypeRegistry messageTypeRegistry) : ControllerBase
    {
        [Route("monitored-endpoints")]
        [HttpGet]
        public ActionResult<MonitoredEndpoint[]> GetAllEndpointsMetrics([FromQuery] int? history = null)
        {
            var metricByInstanceLookup = breakdownProviders.OfType<IProvideBreakdownBy<EndpointInstanceId>>().ToDictionary(i => i.GetType());

            var metricByQueueLookup = breakdownProviders.OfType<IProvideBreakdownBy<EndpointInputQueue>>().ToDictionary(i => i.GetType());

            var endpoints = GetMonitoredEndpoints(endpointRegistry, activityTracker);
            var period = HistoryPeriod.FromMinutes(history ?? DefaultHistory);

            foreach (var metric in InstanceMetrics)
            {
                var store = metricByInstanceLookup[metric.StoreType];
                var intervals = store.GetIntervals(period, DateTime.UtcNow).ToLookup(k => k.Id.EndpointName);

                foreach (var endpoint in endpoints)
                {
                    var values = metric.Aggregate(intervals[endpoint.Name].ToList(), period);

                    endpoint.Metrics.Add(metric.ReturnName, values);
                }
            }

            foreach (var metric in QueueMetrics)
            {
                var store = metricByQueueLookup[metric.StoreType];
                var intervals = store.GetIntervals(period, DateTime.UtcNow).ToLookup(k => k.Id.EndpointName);

                foreach (var endpoint in endpoints)
                {
                    var values = metric.Aggregate(intervals[endpoint.Name].ToList(), period);

                    endpoint.Metrics.Add(metric.ReturnName, values);
                }
            }

            return endpoints;
        }

        [Route("monitored-endpoints/{endpointName}")]
        [HttpGet]
        public ActionResult<MonitoredEndpointDetails> GetSingleEndpointMetrics(string endpointName, [FromQuery] int? history = null)
        {
            var metricByInstanceLookup = breakdownProviders.OfType<IProvideBreakdownBy<EndpointInstanceId>>().ToDictionary(i => i.GetType());

            var metricByQueueLookup = breakdownProviders.OfType<IProvideBreakdownBy<EndpointInputQueue>>().ToDictionary(i => i.GetType());

            var metricByMessageTypeLookup = breakdownProviders.OfType<IProvideBreakdownBy<EndpointMessageType>>().ToDictionary(i => i.GetType());

            var period = HistoryPeriod.FromMinutes(history ?? DefaultHistory);

            var instances = GetMonitoredEndpointInstances(endpointRegistry, endpointName, activityTracker);

            var digest = new MonitoredEndpointDigest();
            var metricDetails = new MonitoredEndpointMetricDetails();

            foreach (var metric in InstanceMetrics)
            {
                var store = metricByInstanceLookup[metric.StoreType];
                var intervals = store.GetIntervals(period, DateTime.UtcNow);

                var intervalsByEndpoint = intervals.ToLookup(k => k.Id.EndpointName);

                var endpointValues = metric.Aggregate(intervalsByEndpoint[endpointName].ToList(), period);

                if (DetailedMetrics.Contains(metric.ReturnName))
                {
                    var details = new MonitoredValuesWithTimings
                    {
                        Points = endpointValues.Points,
                        Average = endpointValues.Average,
                        TimeAxisValues = GetTimeAxisValues(intervalsByEndpoint[endpointName])
                    };

                    metricDetails.Metrics.Add(metric.ReturnName, details);
                }

                var metricDigest = new MonitoredEndpointMetricDigest
                {
                    Latest = endpointValues.Points.LastOrDefault(),
                    Average = endpointValues.Average
                };

                digest.Metrics.Add(metric.ReturnName, metricDigest);

                var intervalsByInstanceId = intervals.ToLookup(k => k.Id);

                foreach (var instance in instances)
                {
                    var instanceId = new EndpointInstanceId(endpointName, instance.Id, instance.Name);

                    var instanceValues = metric.Aggregate(intervalsByInstanceId[instanceId].ToList(), period);

                    instance.Metrics.Add(metric.ReturnName, instanceValues);
                }
            }

            foreach (var metric in QueueMetrics)
            {
                var store = metricByQueueLookup[metric.StoreType];
                var intervals = store.GetIntervals(period, DateTime.UtcNow);

                var intervalsByEndpoint = intervals.ToLookup(k => k.Id.EndpointName);

                var endpointValues = metric.Aggregate(intervalsByEndpoint[endpointName].ToList(), period);

                if (DetailedMetrics.Contains(metric.ReturnName))
                {
                    var details = new MonitoredValuesWithTimings
                    {
                        Points = endpointValues.Points,
                        Average = endpointValues.Average,
                        TimeAxisValues = GetTimeAxisValues(intervalsByEndpoint[endpointName])
                    };

                    metricDetails.Metrics.Add(metric.ReturnName, details);
                }

                var metricDigest = new MonitoredEndpointMetricDigest
                {
                    Latest = endpointValues.Points.LastOrDefault(),
                    Average = endpointValues.Average
                };

                digest.Metrics.Add(metric.ReturnName, metricDigest);
            }

            var messageTypes = GetMonitoredMessageTypes(messageTypeRegistry.GetForEndpointName(endpointName));

            foreach (var metric in MessageTypeMetrics)
            {
                var store = metricByMessageTypeLookup[metric.StoreType];
                var intervals = store.GetIntervals(period, DateTime.UtcNow).ToLookup(k => k.Id);

                foreach (var messageType in messageTypes)
                {
                    var values = metric.Aggregate(intervals[new EndpointMessageType(endpointName, messageType.Id)].ToList(), period);

                    messageType.Metrics.Add(metric.ReturnName, values);
                }
            }

            var data = new MonitoredEndpointDetails
            {
                Digest = digest,
                Instances = instances,
                MessageTypes = messageTypes,
                MetricDetails = metricDetails
            };

            return data;
        }

        [Route("monitored-instance/{endpointName}/{instanceId}")]
        [HttpDelete]
        public IActionResult DeleteEndpointInstance(string endpointName, string instanceId)
        {
            endpointRegistry.RemoveEndpointInstance(endpointName, instanceId);
            activityTracker.Remove(new EndpointInstanceId(endpointName, instanceId));

            return Ok();
        }

        [Route("monitored-endpoints/disconnected")]
        [HttpGet]
        public ActionResult<int> DisconnectedEndpointCount()
        {
            var disconnectedEndpointCount = endpointRegistry
                .GetGroupedByEndpointName()
                .Count(endpoint => endpoint.Value.All(activityTracker.IsStale));

            return disconnectedEndpointCount;
        }

        static DateTime[] GetTimeAxisValues<T>(IEnumerable<IntervalsStore<T>.IntervalsBreakdown> intervals)
        {
            return intervals
                .SelectMany(ib => ib.Intervals.Select(x => x.IntervalStart.ToUniversalTime()))
                .Distinct()
                .OrderBy(i => i)
                .ToArray();
        }

        static MonitoredEndpointInstance[] GetMonitoredEndpointInstances(EndpointRegistry endpointRegistry, string endpointName, EndpointInstanceActivityTracker activityTracker)
        {
            return endpointRegistry.GetForEndpointName(endpointName)
                .Select(endpointInstance => new MonitoredEndpointInstance
                {
                    Id = endpointInstance.InstanceId,
                    Name = endpointInstance.InstanceName,
                    IsStale = activityTracker.IsStale(endpointInstance)
                }).ToArray();
        }

        static MonitoredEndpoint[] GetMonitoredEndpoints(EndpointRegistry endpointRegistry, EndpointInstanceActivityTracker activityTracker)
        {
            return endpointRegistry.GetGroupedByEndpointName()
                .Select(endpoint => new MonitoredEndpoint
                {
                    Name = endpoint.Key,
                    EndpointInstanceIds = endpoint.Value.Select(i => i.InstanceId).ToArray(),
                    IsStale = endpoint.Value.Any(activityTracker.IsStale),
                    ConnectedCount = endpoint.Value.Count(id => !activityTracker.IsStale(id)),
                    DisconnectedCount = endpoint.Value.Count(activityTracker.IsStale)
                })
                .ToArray();
        }

        static MonitoredMetric<BreakdownT> CreateMetric<BreakdownT, StoreT>(string name, Aggregation<BreakdownT> aggregation)
            where StoreT : IProvideBreakdownBy<BreakdownT>
        {
            return new MonitoredMetric<BreakdownT>
            {
                StoreType = typeof(StoreT),
                ReturnName = name,
                Aggregate = aggregation
            };
        }

        static MonitoredEndpointMessageType[] GetMonitoredMessageTypes(IEnumerable<EndpointMessageType> messageTypes)
        {
            return messageTypes.Select(mt => MonitoredEndpointMessageTypeParser.Parse(mt.MessageType))
                .ToArray();
        }

        public const int DefaultHistory = 5;

        static HashSet<string> DetailedMetrics =
        [
            "Throughput",
            "QueueLength",
            "ProcessingTime",
            "CriticalTime",
            "Retries"
        ];

        static MonitoredMetric<EndpointInstanceId>[] InstanceMetrics =
        {
            CreateMetric<EndpointInstanceId, ProcessingTimeStore>("ProcessingTime", Aggregator.ToAverages),
            CreateMetric<EndpointInstanceId, CriticalTimeStore>("CriticalTime", Aggregator.ToAverages),
            CreateMetric<EndpointInstanceId, RetriesStore>("Retries", Aggregator.ToTotalMeasurementsPerSecond),
            CreateMetric<EndpointInstanceId, ProcessingTimeStore>("Throughput", Aggregator.ToTotalMeasurementsPerSecond)
        };

        static MonitoredMetric<EndpointMessageType>[] MessageTypeMetrics =
        {
            CreateMetric<EndpointMessageType, ProcessingTimeStore>("ProcessingTime", Aggregator.ToAverages),
            CreateMetric<EndpointMessageType, CriticalTimeStore>("CriticalTime", Aggregator.ToAverages),
            CreateMetric<EndpointMessageType, RetriesStore>("Retries", Aggregator.ToTotalMeasurementsPerSecond),
            CreateMetric<EndpointMessageType, ProcessingTimeStore>("Throughput", Aggregator.ToTotalMeasurementsPerSecond)
        };

        static MonitoredMetric<EndpointInputQueue>[] QueueMetrics =
        {
            CreateMetric<EndpointInputQueue, QueueLengthStore>("QueueLength", Aggregator.ToRoundedSumOfBreakdownAverages)
        };

        delegate MonitoredValues Aggregation<T>(List<IntervalsStore<T>.IntervalsBreakdown> intervals, HistoryPeriod period);

        class MonitoredMetric<T>
        {
            public Type StoreType { get; set; }
            public string ReturnName { get; set; }
            public Aggregation<T> Aggregate { get; set; }
        }
    }
}
