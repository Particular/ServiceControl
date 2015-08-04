namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Linq;
    using Raven.Client.Indexes;

    public class FailedMessageViewTransformer : AbstractTransformerCreationTask<FailedMessage>
    {
        public FailedMessageViewTransformer()
        {
            TransformResults = failures => from failure in failures
                let rec = failure.ProcessingAttempts.Last()
                select new
                {
                    Id = failure.UniqueMessageId,
                    MessageType = rec.MessageMetadata["MessageType"],
                    IsSystemMessage = (bool)rec.MessageMetadata["IsSystemMessage"],
                    SendingEndpoint = rec.MessageMetadata["SendingEndpoint"],
                    ReceivingEndpoint = rec.MessageMetadata["ReceivingEndpoint"],
                    TimeSent = (DateTime?)rec.MessageMetadata["TimeSent"],
                    MessageId = rec.MessageMetadata["MessageId"],
                    rec.FailureDetails.Exception,
                    NumberOfProcessingAttempts = failure.ProcessingAttempts.Count(),
                    failure.Status,
                };
        }
    }
}