namespace ServiceControl.EndpointPlugin.Operations.PerformanceData
{
    using System;
    using System.Collections.Generic;

    public class EndpointPerformance
    {
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

        Dictionary<string, List<DataPoint>> performanceData;
    }

    public class DataPoint
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }
}
