//NOTE: this class needs to stay in NServiceBus.Metrics to be properly deserialized

namespace NServiceBus.Metrics
{
    using System.Text.Json.Nodes;

    /// <summary>
    /// The reporting message.
    /// </summary>
    public class MetricReport : IMessage
    {
        /// <summary>
        /// Serialized raw data of the report.
        /// </summary>
        public JsonNode Data { get; set; }
    }
}