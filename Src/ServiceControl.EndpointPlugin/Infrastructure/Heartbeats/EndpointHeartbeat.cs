namespace ServiceControl.EndpointPlugin.Infrastructure.Heartbeats
{
    using System;
    using System.Collections.Generic;

    public class EndpointHeartbeat
    {
        public DateTime ExecutedAt { get; set; }


        public Dictionary<string, string> Configuration
        {
            get
            {
                if (configuration == null)
                {
                    configuration = new Dictionary<string, string>();
                }

                return configuration;
            }
            set { configuration = value; }
        }


        public Dictionary<string, List<DataPoint>> PerformanceData
        {
            get
            {
                if (performanceData == null)
                {
                    performanceData = new Dictionary<string, List<DataPoint>>();
                }

                return performanceData;
            }
            set { performanceData = value; }
        }

        Dictionary<string, string> configuration;
        Dictionary<string, List<DataPoint>> performanceData;
    }

    public class DataPoint
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }
}