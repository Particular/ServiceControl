namespace ServiceControl.Notifications.Api
{
    using Newtonsoft.Json;

    public class UpdateEmailNotificationsSettingsRequest
    {
        [JsonProperty(PropertyName = "smtp_server")]
        public string SmtpServer { get; set; }

        [JsonProperty(PropertyName = "smtp_port")]
        public int SmtpPort { get; set; }

        [JsonProperty(PropertyName = "authorization_account")]
        public string AuthorizationAccount { get; set; }

        [JsonProperty(PropertyName = "authorization_password")]
        public string AuthorizationPassword { get; set; }

        [JsonProperty(PropertyName = "enable_tls")]
        public bool EnableTLS { get; set; }

        [JsonProperty(PropertyName = "from")]
        public string From { get; set; }

        [JsonProperty(PropertyName = "to")]
        public string To { get; set; }
    }
}