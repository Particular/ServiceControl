namespace ServiceControl.MessageFailures.Api
{
    using Infrastructure.RavenDB.Indexes;

    public class FailedMessageView : CommonResult
    {
        public string ErrorMessageId { get; set; }

        public string ExceptionMessage { get; set; }

        public string MessageId { get; set; }
        public int NumberOfProcessingAttempts { get; set; }

        public FailedMessageStatus Status { get; set; }

    }
}