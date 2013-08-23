namespace ServiceBus.Management.BusinessMonitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using NServiceBus;
    using ServiceControl.EndpointPlugin.Infrastructure.Heartbeats;

    public class EndpointSLAMonitoring:INeedInitialization
    {
        public IBus Bus { get; set; }

        public TimeSpan GetSLAFor(string endpoint)
        {
            return endpointsBeeingMonitored[endpoint].CurrentSLA;
        }

        public void RegisterSLA(string endpoint, TimeSpan sla)
        {
            endpointsBeeingMonitored.AddOrUpdate(endpoint, new SLAStatus(sla), (name, e) =>
                {
                    e.CurrentSLA = sla;
                    return e;
                });
        }

        public void ReportCriticalTimeMeasurements(string endpoint, List<DataPoint> dataPoints)
        {
            var currentStatus = endpointsBeeingMonitored.AddOrUpdate(endpoint, new SLAStatus(dataPoints), (name, e) =>
            {
                e.CriticalTimeValues.AddRange(dataPoints);
                return e;
            });

            if (currentStatus.SLABreached()) //todo: debounce
                Bus.InMemory.Raise<EndpointSLABreached>(e => e.Endpoint = endpoint);
        }

        ConcurrentDictionary<string, SLAStatus> endpointsBeeingMonitored = new ConcurrentDictionary<string, SLAStatus>();


        public class SLAStatus
        {
            public SLAStatus(TimeSpan currentSLA)
            {
                CurrentSLA = currentSLA;
                CriticalTimeValues = new List<DataPoint>();
            }

            public SLAStatus(List<DataPoint> dataPoints)
            {
                CurrentSLA = TimeSpan.Zero;
                CriticalTimeValues = dataPoints;

            }

            public TimeSpan CurrentSLA { get; set; }

            public List<DataPoint> CriticalTimeValues { get;private set; }


            public bool SLABreached()
            {
                if (CurrentSLA == TimeSpan.Zero)
                    return false;

                if (!CriticalTimeValues.Any())
                    return false;

                return CurrentSLA < TimeSpan.FromSeconds(CriticalTimeValues.Last().Value);
            }
        }

        public void Init()
        {
            Configure.Component<EndpointSLAMonitoring>(DependencyLifecycle.SingleInstance);
        }
    }
}