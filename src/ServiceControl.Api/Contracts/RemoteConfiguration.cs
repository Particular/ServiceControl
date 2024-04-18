namespace ServiceControl.Api.Contracts
{
    using System.Text.Json.Nodes;

    public class RemoteConfiguration
    {
        public string ApiUri { get; set; }
        public string Version { get; set; }
        public string Status { get; set; }
        public JsonNode Configuration { get; set; }
    }
}