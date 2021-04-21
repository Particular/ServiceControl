namespace ServiceControl.Alerting
{
    public class AlertingSettings
    {
        public const string SingleDocumentId = "AlertingSettings/All";

        public string Id { get; set; }

        public string SmtpServer { get; set; }

        public int? SmtpPort { get; set; }

        public bool AuthenticationEnabled { get; set; }

        public string AuthenticationAccount { get; set; }

        public string AuthenticationPassword { get; set; }

        public bool EnableSSL { get; set; }

        public bool AlertingEnabled { get; set; }

        public string To { get; set; }

        public string From { get; set; }
    }
}