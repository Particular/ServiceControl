﻿namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Linq;

    class FailedMessageViewTransformer : AbstractTransformerCreationTask<FailedMessage> // https://ravendb.net/docs/article-page/4.2/csharp/migration/client-api/session/querying/transformers
    {
        public FailedMessageViewTransformer()
        {
            TransformResults = failures => from failure in failures
                                           let rec = failure.ProcessingAttempts.Last()
                                           let edited = rec.Headers["ServiceControl.EditOf"] != null
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
                                               rec.FailureDetails.TimeOfFailure,
                                               LastModified = MetadataFor(failure)["@last-modified"].Value<DateTime>(),
                                               Edited = edited,
                                               EditOf = edited ? rec.Headers["ServiceControl.EditOf"] : ""
                                           };
        }

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

        static string transformerName;
    }
}