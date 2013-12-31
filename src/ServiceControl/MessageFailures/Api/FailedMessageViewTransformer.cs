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
                    MessageType = rec.MessageMetadata["MessageType"],
                    IsSystemMessage = rec.MessageMetadata["IsSystemMessage"],
                    SendingEndpoint = rec.MessageMetadata["SendingEndpoint"],
                    ReceivingEndpoint = rec.MessageMetadata["ReceivingEndpoint"],
                    TimeSent = rec.MessageMetadata["TimeSent"],
                    MessageId = rec.MessageMetadata["MessageId"],
                    rec.FailureDetails.Exception,
                    NumberOfProcessingAttempts = failure.ProcessingAttempts.Count(),
                    failure.Status,
                };
        }
    }
}