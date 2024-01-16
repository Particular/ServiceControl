namespace ServiceControl.Notifications.Api
{
    using System.Text.Json.Serialization;

    public class ToggleEmailNotifications
    {
        // TODO In theory this is not needed because we are using the snake case converter
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
    }
}