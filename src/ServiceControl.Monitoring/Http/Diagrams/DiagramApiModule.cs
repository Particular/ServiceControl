namespace ServiceControl.Monitoring.Http.Diagrams
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Infrastructure;
    using Nancy;
    using QueueLength;
    using Timings;

    /// <summary>
    /// Exposes ServiceControl.Monitoring metrics needed for in endpoint overview page.
    /// </summary>
    public class MonitoredEndpointsModule : ApiModule
    {
        /// <summary>
        /// Initializes the metric API module.
        /// </summary>
        public MonitoredEndpointsModule(IProvideBreakdown[] breakdownProviders, EndpointRegistry endpointRegistry, EndpointInstanceActivityTracker activityTracker, MessageTypeRegistry messageTypeRegistry)
        {
            var metricByInstanceLookup = breakdownProviders.OfType<IProvideBreakdownBy<EndpointInstanceId>>().ToDictionary(i => i.GetType());

            var metricByQueueLookup = breakdownProviders.OfType<IProvideBreakdownBy<EndpointInputQueue>>().ToDictionary(i => i.GetType());

            var metricByMessageTypeLookup = breakdownProviders.OfType<IProvideBreakdownBy<EndpointMessageType>>().ToDictionary(i => i.GetType());

            var instanceMetrics = new[]
            {
                CreateMetric<EndpointInstanceId, ProcessingTimeStore>("ProcessingTime", Aggregator.ToAverages),
                CreateMetric<EndpointInstanceId, CriticalTimeStore>("CriticalTime", Aggregator.ToAverages),
                CreateMetric<EndpointInstanceId, RetriesStore>("Retries", Aggregator.ToTotalMeasurementsPerSecond),
                CreateMetric<EndpointInstanceId, ProcessingTimeStore>("Throughput", Aggregator.ToTotalMeasurementsPerSecond)
            };

            var queueMetrics = new[]
            {
                CreateMetric<EndpointInputQueue, QueueLengthStore>("QueueLength", Aggregator.ToRoundedSumOfBreakdownAverages)
            };

            var messageTypeMetrics = new[]
            {
                CreateMetric<EndpointMessageType, ProcessingTimeStore>("ProcessingTime", Aggregator.ToAverages),
                CreateMetric<EndpointMessageType, CriticalTimeStore>("CriticalTime", Aggregator.ToAverages),
                CreateMetric<EndpointMessageType, RetriesStore>("Retries", Aggregator.ToTotalMeasurementsPerSecond),
                CreateMetric<EndpointMessageType, ProcessingTimeStore>("Throughput", Aggregator.ToTotalMeasurementsPerSecond)
            };

            var detailedMetrics = new HashSet<string>
            {
                "Throughput",
                "QueueLength",
                "ProcessingTime",
                "CriticalTime",
                "Retries"
            };

            Get["/monitored-endpoints"] = parameters =>
            {
                var endpoints = GetMonitoredEndpoints(endpointRegistry, activityTracker);
                var period = ExtractHistoryPeriod();

                foreach (var metric in instanceMetrics)
                {
                    var store = metricByInstanceLookup[metric.StoreType];
                    var intervals = store.GetIntervals(period, DateTime.UtcNow).ToLookup(k => k.Id.EndpointName);

                    foreach (var endpoint in endpoints)
                    {
                        var values = metric.Aggregate(intervals[endpoint.Name].ToList(), period);

                        endpoint.Metrics.Add(metric.ReturnName, values);
                    }
                }

                foreach (var metric in queueMetrics)
                {
                    var store = metricByQueueLookup[metric.StoreType];
                    var intervals = store.GetIntervals(period, DateTime.UtcNow).ToLookup(k => k.Id.EndpointName);

                    foreach (var endpoint in endpoints)
                    {
                        var values = metric.Aggregate(intervals[endpoint.Name].ToList(), period);

                        endpoint.Metrics.Add(metric.ReturnName, values);
                    }
                }

                return Negotiate.WithModel(endpoints);
            };

            Get["/monitored-endpoints/{endpointName}"] = parameters =>
            {
                var endpointName = (string) parameters.EndpointName;
                var period = ExtractHistoryPeriod();

                var instances = GetMonitoredEndpointInstances(endpointRegistry, endpointName, activityTracker);

                var digest = new MonitoredEndpointDigest();
                var metricDetails = new MonitoredEndpointMetricDetails();

                foreach (var metric in instanceMetrics)
                {
                    var store = metricByInstanceLookup[metric.StoreType];
                    var intervals = store.GetIntervals(period, DateTime.UtcNow);

                    var intervalsByEndpoint = intervals.ToLookup(k => k.Id.EndpointName);

                    var endpointValues = metric.Aggregate(intervalsByEndpoint[endpointName].ToList(), period);

                    if (detailedMetrics.Contains(metric.ReturnName))
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

                foreach (var metric in queueMetrics)
                {
                    var store = metricByQueueLookup[metric.StoreType];
                    var intervals = store.GetIntervals(period, DateTime.UtcNow);

                    var intervalsByEndpoint = intervals.ToLookup(k => k.Id.EndpointName);

                    var endpointValues = metric.Aggregate(intervalsByEndpoint[endpointName].ToList(), period);

                    if (detailedMetrics.Contains(metric.ReturnName))
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

                foreach (var metric in messageTypeMetrics)
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

                return Negotiate.WithModel(data);
            };
        }

        static DateTime[] GetTimeAxisValues<T>(IEnumerable<IntervalsStore<T>.IntervalsBreakdown> intervals)
        {
            return intervals
                .SelectMany(ib => ib.Intervals.Select(x => x.IntervalStart.ToUniversalTime()))
                .Distinct()
                .OrderBy(i => i)
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

        HistoryPeriod ExtractHistoryPeriod()
        {
            return HistoryPeriod.FromMinutes(Request.Query["history"] == null || Request.Query["history"] == "undefined" ? DefaultHistory : (int) Request.Query["history"]);
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
                    IsStale = endpoint.Value.Any(activityTracker.IsStale)
                })
                .ToArray();
        }

        static MonitoredEndpointMessageType[] GetMonitoredMessageTypes(IEnumerable<EndpointMessageType> messageTypes)
        {
            return messageTypes.Select(mt => MonitoredEndpointMessageTypeParser.Parse(mt.MessageType))
                               .ToArray();
        }

        public const int DefaultHistory = 5;

        delegate MonitoredValues Aggregation<T>(List<IntervalsStore<T>.IntervalsBreakdown> intervals, HistoryPeriod period);

        class MonitoredMetric<T>
        {
            public Type StoreType { get; set; }
            public string ReturnName { get; set; }
            public Aggregation<T> Aggregate { get; set; }
        }
    }
}