namespace ServiceControl.CompositeViews.Messages
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class MessagesBodyTransformer : AbstractTransformerCreationTask<MessagesViewTransformer.Result>
    {
        public class Result
        {
            public string Id { get; set; }
            public string BodyString { get; set; }
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
                    Id = message.UniqueMessageId,
                    BodyString = metadata["SearchableBody"],
                };
        }
    }
}