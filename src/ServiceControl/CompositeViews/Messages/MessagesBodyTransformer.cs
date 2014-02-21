namespace ServiceControl.CompositeViews.Messages
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class MessagesBodyTransformer : AbstractTransformerCreationTask<MessagesViewTransformer.Result>
    {
        public class Result
        {
            public string MessageId { get; set; }
            public string Body { get; set; }
            public string ContentType { get; set; }
            public int BodySize { get; set; }
        }

        public MessagesBodyTransformer()
        {
            TransformResults = messages => from message in messages
                let metadata =
                    message.ProcessingAttempts != null
                        ? message.ProcessingAttempts.Last().MessageMetadata
                        : message.MessageMetadata
                select new
                {
                    MessageId = metadata["MessageId"],
                    Body = metadata["Body"],
                    BodySize = (int)metadata["ContentLength"],
                    ContentType = metadata["ContentType"],
                };
        }
    }
}