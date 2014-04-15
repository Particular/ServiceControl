namespace ServiceControl.BusinessMonitoring
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using Contracts.Operations;
    using NServiceBus;
    using Contracts.BusinessMonitoring;

    public class EndpointSLAMonitoring : INeedInitialization
    {
        public IBus Bus { get; set; }

        public void Init()
        {
            Configure.Component<EndpointSLAMonitoring>(DependencyLifecycle.SingleInstance);
        }

        public TimeSpan GetSLAFor(string endpoint)
        {
            SLAStatus status;

            if (endpointsBeingMonitored.TryGetValue(endpoint, out status))
            {
                return status.CurrentSLA;
            }

            return TimeSpan.Zero;
        }

        public void RegisterSLA(string endpoint, TimeSpan sla)
        {
            endpointsBeingMonitored.AddOrUpdate(endpoint, new SLAStatus(sla), (name, e) =>
            {
                e.CurrentSLA = sla;
                return e;
            });
        }

        public void ReportCriticalTimeMeasurements(string endpoint, List<DataPoint> dataPoints)
        {
            var currentStatus = endpointsBeingMonitored.AddOrUpdate(endpoint, new SLAStatus(dataPoints), (name, e) =>
            {
                e.CriticalTimeValues.AddRange(dataPoints);
                return e;
            });

            if (currentStatus.SLABreached()) //todo: debounce
            {
                Bus.InMemory.Raise<EndpointSLABreached>(e => e.Endpoint = endpoint);
            }
        }

        readonly ConcurrentDictionary<string, SLAStatus> endpointsBeingMonitored =
            new ConcurrentDictionary<string, SLAStatus>();


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

            public List<DataPoint> CriticalTimeValues { get; private set; }


            public bool SLABreached()
            {
                if (CurrentSLA == TimeSpan.Zero)
                {
                    return false;
                }

                if (!CriticalTimeValues.Any())
                {
                    return false;
                }

                return CurrentSLA < TimeSpan.FromSeconds(CriticalTimeValues.Last().Value);
            }
        }
    }
}