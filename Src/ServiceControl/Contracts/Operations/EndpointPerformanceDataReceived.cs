namespace ServiceControl.Contracts.Operations
{
    using System;
    using System.Collections.Generic;
    using NServiceBus;

    public class EndpointPerformanceDataReceived:IEvent
    {
        public string Endpoint { get; set; }


        public Dictionary<string, List<DataPoint>> Data
        {
            get
            {
                if (data == null)
                {
                    data = new Dictionary<string, List<DataPoint>>();
                }

                return data;
            }
            set { data = value; }
        }

        Dictionary<string, List<DataPoint>> data;

     
    }

    public class DataPoint
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }
}