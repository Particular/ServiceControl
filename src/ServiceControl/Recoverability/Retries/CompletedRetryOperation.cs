
namespace ServiceControl.Recoverability
{
    using System;

    public class CompletedRetryOperation
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public DateTime CompletionTime { get; set; }
        public string Originator { get; set; }
    }
}