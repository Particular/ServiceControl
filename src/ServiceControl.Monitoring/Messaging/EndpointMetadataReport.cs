//NOTE: this class needs to stay in NServiceBus.Metrics to be properly deserialized
namespace NServiceBus.Metrics
{
    public class EndpointMetadataReport : IMessage
    {
        public int PluginVersion { get; set; }
        public string LocalAddress { get; set; }
    }
}