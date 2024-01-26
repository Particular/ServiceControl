namespace ServiceControl.Monitoring.Http.Diagrams
{
    using System;
    using System.Text.Json.Serialization;

    [JsonDerivedType(typeof(MonitoredValuesWithTimings))]
    public class MonitoredValues
    {
        public double? Average { get; set; }
        public double[] Points { get; set; }
    }

    public class MonitoredValuesWithTimings : MonitoredValues
    {
        public DateTime[] TimeAxisValues { get; set; }
    }
}