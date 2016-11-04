namespace ServiceControl.Recoverability
{
    using Raven.Imports.Newtonsoft.Json;

    public class RetryBatchGroup
    {
        public string RequestId { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] //default to RetryType.Unknown for backwards compatability
        public RetryType RetryType { get; set; }

        public RetryBatchStatus Status { get; set; }

        public int InitialBatchSize { get; set; }

        public string Originator{ get; set; }
    }
}