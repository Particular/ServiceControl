namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class FailedMessageViewTransformer : AbstractTransformerCreationTask<FailedMessage>
    {
        public FailedMessageViewTransformer()
        {
            TransformResults = failures => from failure in failures
                let rec = failure.MostRecentAttempt
                select new
                {
                    failure.Id,
                    MessageType = rec.MessageMetadata["MessageType"].Value,
                    IsSystemMessage = rec.MessageMetadata["IsSystemMessage"].Value,
                    SendingEndpoint = rec.MessageMetadata["SendingEndpoint"].Value,
                    ReceivingEndpoint = rec.MessageMetadata["ReceivingEndpoint"].Value,
                    TimeSent = rec.MessageMetadata["TimeSent"].Value,
                    MessageId = rec.MessageMetadata["MessageId"].Value.ToString(),
                    rec.FailureDetails.Exception,
                    NumberOfProcessingAttempts = failure.ProcessingAttempts.Count(),
                    failure.Status,
                };
        }
    }
}