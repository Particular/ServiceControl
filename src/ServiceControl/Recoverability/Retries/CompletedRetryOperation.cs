
namespace ServiceControl.Recoverability
{
    using System;

    public class CompletedRetryOperation
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public DateTime CompletionDate { get; set; }
    }
}