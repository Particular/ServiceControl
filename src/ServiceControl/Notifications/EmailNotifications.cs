namespace ServiceControl.Notifications
{
    public class EmailNotifications
    {
        public bool Enabled { get; set; }

        public string SmtpServer { get; set; }

        public int? SmtpPort { get; set; }

        public string AuthenticationAccount { get; set; }

        public string AuthenticationPassword { get; set; }

        public bool EnableTLS { get; set; }

        public string To { get; set; }

        public string From { get; set; }
    }
}