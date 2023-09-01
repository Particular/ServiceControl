namespace ServiceControl.Persistence
{
    using System;

    public class RetryBatchGroup
    {
        public string RequestId { get; set; }

        // [Raven.Imports.Newtonsoft.Json.JsonProperty(NullValueHandling = NullValueHandling.Ignore)] //default to RetryType.Unknown for backwards compatability
        // TODO: Need to fix ethe JsonProperty, maybe RavenDB has a method to specify metatdata or use a mapper/transformation
        // THEORY: RetryType.Unknown is value 0 so it should default to that anyway
        public RetryType RetryType { get; set; }

        public bool HasStagingBatches { get; set; }

        public bool HasForwardingBatches { get; set; }

        public int InitialBatchSize { get; set; }

        public string Originator { get; set; }

        public string Classifier { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime Last { get; set; }
    }
}