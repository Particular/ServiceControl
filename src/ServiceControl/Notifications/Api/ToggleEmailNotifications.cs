namespace ServiceControl.Notifications.Api
{
    using System.Text.Json.Serialization;

    public class ToggleEmailNotifications
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }
}