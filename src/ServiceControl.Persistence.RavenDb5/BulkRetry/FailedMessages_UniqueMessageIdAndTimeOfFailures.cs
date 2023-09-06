namespace ServiceControl.Recoverability
{
    using System;
    using System.Linq;
    using MessageFailures;

    class FailedMessages_UniqueMessageIdAndTimeOfFailures : AbstractTransformerCreationTask<FailedMessage> // https://ravendb.net/docs/article-page/4.2/csharp/migration/client-api/session/querying/transformers
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