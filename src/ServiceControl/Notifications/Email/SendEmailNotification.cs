namespace ServiceControl.Notifications.Email
{
    using NServiceBus;

    public class SendEmailNotification : ICommand
    {
        public string Subject { get; set; }

        public string Body { get; set; }

        public bool FailureNotification { get; set; }
    }
}