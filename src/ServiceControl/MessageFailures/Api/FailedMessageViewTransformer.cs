namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Linq;
    using Raven.Client.Indexes;

    public class FailedMessageViewTransformer : AbstractTransformerCreationTask<FailedMessage>
    {
        private static string transformerName;
        
        public static string Name
        {
            get
            {
                if (transformerName == null)
                {
                    transformerName = new FailedMessageViewTransformer().TransformerName;
                }

                return transformerName;
            }
        }
        
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
                    QueueAddress = rec.FailureDetails.AddressOfFailingEndpoint,
                    NumberOfProcessingAttempts = failure.ProcessingAttempts.Count,
                    failure.Status,
                    TimeOfFailure = rec.FailureDetails.TimeOfFailure,
                    LastModified = MetadataFor(failure)["Last-Modified"].Value<DateTime>()
                };
        }
    }
}