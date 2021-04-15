namespace ServiceControl.Alerting.Api
{
    using Newtonsoft.Json;

    public class UpdateAlertingSettingsRequest
    {
        [JsonProperty(PropertyName = "smtp_server")]
        public string SmtpServer { get; set; }

        [JsonProperty(PropertyName = "smtp_port")]
        public int SmtpPort { get; set; }

        [JsonProperty(PropertyName = "authorization_account")]
        public string AuthorizationAccount { get; set; }

        [JsonProperty(PropertyName = "authorization_password")]
        public string AuthorizationPassword { get; set; }

        [JsonProperty(PropertyName = "enable_ssl")]
        public bool EnableSSL { get; set; }

        [JsonProperty(PropertyName = "alerting_enabled")]
        public bool AlertingEnabled { get; set; }
    }
}