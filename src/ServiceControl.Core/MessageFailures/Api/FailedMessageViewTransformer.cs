namespace ServiceControl.MessageFailures.Api
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class FailedMessageViewTransformer : AbstractTransformerCreationTask<MessageFailureHistory>
    {
        public FailedMessageViewTransformer()
        {
            TransformResults = failures => from failure in failures
                let rec = failure.ProcessingAttempts.Last()
                select new
                {
                    Id = failure.UniqueMessageId,
                    MessageType = rec.MessageType,
                    IsSystemMessage = rec.IsSystemMessage,
                    SendingEndpoint = rec.SendingEndpoint,
                    ReceivingEndpoint = rec.ProcessingEndpoint,
                    TimeSent = rec.TimeSent,
                    MessageId = rec.MessageId,
                    rec.FailureDetails.Exception,
                    NumberOfProcessingAttempts = failure.ProcessingAttempts.Count(),
                    failure.Status,
                };
        }
    }
}