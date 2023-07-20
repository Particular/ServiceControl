namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using MessageFailures;
    using Raven.Client.Indexes;

    class FailedMessages_UniqueMessageIdAndTimeOfFailures : AbstractTransformerCreationTask<FailedMessage>
    {
        public FailedMessages_UniqueMessageIdAndTimeOfFailures()
        {
            TransformResults = failedMessages => from failedMessage in failedMessages
                                                 select new
                                                 {
                                                     failedMessage.UniqueMessageId,
                                                     LatestTimeOfFailure = failedMessage.ProcessingAttempts.Max(x => x.FailureDetails.TimeOfFailure)
                                                 };
        }

        public struct Result
        {
            public string UniqueMessageId { get; set; }

            public DateTime LatestTimeOfFailure { get; set; }
        }
    }
}