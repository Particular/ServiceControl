namespace ServiceControl.CompositeViews.Messages
{
    using System.Collections.Generic;
    using System.Linq;
    using MessageFailures;
    using Raven.Client.Indexes;

    public class MessagesBodyTransformer : AbstractTransformerCreationTask<MessagesBodyTransformer.Input>
    {
        public MessagesBodyTransformer()
        {
            TransformResults = messages => from message in messages
                                           let attempt = message.ProcessingAttempts != null
                                               ? message.ProcessingAttempts.Last()
                                               : new FailedMessage.ProcessingAttempt()
                                           let metadata = message.ProcessingAttempts != null
                                               ? message.ProcessingAttempts.Last().MessageMetadata
                                               : message.MessageMetadata
                                           select new
                                           {
                                               MessageId = metadata["MessageId"],
                                               Body = !string.IsNullOrEmpty(attempt.Body) ? attempt.Body : metadata["Body"],
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