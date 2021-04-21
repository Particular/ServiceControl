namespace ServiceControl.Alerting.Api
{
    using Newtonsoft.Json;

    public class ToggleEmailNotifications
    {
        [JsonProperty(PropertyName = "enabled")]
        public bool Enabled { get; set; }
    }
}