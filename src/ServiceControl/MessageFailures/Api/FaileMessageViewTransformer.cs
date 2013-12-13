namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class FaileMessageViewTransformer : AbstractTransformerCreationTask<FailedMessage>
    {
        public FaileMessageViewTransformer()
        {
            TransformResults = failures => from failure in failures
                select new
                {
                    Id = failure.Id,
                    MessageType = failure.MostRecentAttempt.MessageMetadata["MessageType"].Value,
                    IsSystemMessage = failure.MostRecentAttempt.MessageMetadata["IsSystemMessage"].Value,
                    SendingEndpoint = failure.MostRecentAttempt.MessageMetadata["SendingEndpoint"].Value,
                    ReceivingEndpoint = failure.MostRecentAttempt.MessageMetadata["ReceivingEndpoint"].Value,
                    TimeSent = failure.MostRecentAttempt.MessageMetadata["TimeSent"].Value,
                    MessageId = failure.MostRecentAttempt.MessageMetadata["MessageId"].Value.ToString(),
                    Exception = failure.MostRecentAttempt.FailureDetails.Exception,
                    NumberOfProcessingAttempts = failure.ProcessingAttempts.Count(),
                    Status = failure.Status,
                };
        }

    }
}