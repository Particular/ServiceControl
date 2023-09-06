namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControl.MessageFailures;

    class MessagesBodyTransformer : AbstractTransformerCreationTask<MessagesBodyTransformer.Input> // https://ravendb.net/docs/article-page/4.2/csharp/migration/client-api/session/querying/transformers
    {
        public MessagesBodyTransformer()
        {
            TransformResults = messages => from message in messages
                                           let metadata = message.ProcessingAttempts != null
                                               ? message.ProcessingAttempts.Last().MessageMetadata
                                               : message.MessageMetadata
                                           let body = message.ProcessingAttempts != null
                                               ? message.ProcessingAttempts.Last().Body ?? metadata["Body"]
                                               : metadata["Body"]
                                           select new
                                           {
                                               MessageId = metadata["MessageId"],
                                               Body = body,
                                               BodySize = (int)metadata["ContentLength"],
                                               ContentType = metadata["ContentType"],
                                               BodyNotStored = (bool)metadata["BodyNotStored"]
                                           };
        }

        public static string Name
        {
            get
            {
                if (transformerName == null)
                {
                    transformerName = new MessagesBodyTransformer().TransformerName;
                }

                return transformerName;
            }
        }

        static string transformerName;

        public class Input
        {
            public Dictionary<string, object> MessageMetadata { get; set; }
            public List<FailedMessage.ProcessingAttempt> ProcessingAttempts { get; set; }
        }

        public class Result
        {
            public string MessageId { get; set; }
            public string Body { get; set; }
            public string ContentType { get; set; }
            public int BodySize { get; set; }
            public bool BodyNotStored { get; set; }
        }
    }
}