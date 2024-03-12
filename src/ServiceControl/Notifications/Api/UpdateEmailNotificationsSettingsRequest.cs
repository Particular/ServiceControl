namespace ServiceControl.Notifications.Api
{
    using System.Text.Json.Serialization;

    public class UpdateEmailNotificationsSettingsRequest
    {
        [JsonPropertyName("smtp_server")]
        public string SmtpServer { get; set; }

        [JsonPropertyName("smtp_port")]
        public int SmtpPort { get; set; }

        [JsonPropertyName("authorization_account")]
        public string AuthorizationAccount { get; set; }

        [JsonPropertyName("authorization_password")]
        public string AuthorizationPassword { get; set; }

        [JsonPropertyName("enable_tls")]
        public bool EnableTLS { get; set; }

        [JsonPropertyName("from")]
        public string From { get; set; }

        [JsonPropertyName("to")]
        public string To { get; set; }
    }
}