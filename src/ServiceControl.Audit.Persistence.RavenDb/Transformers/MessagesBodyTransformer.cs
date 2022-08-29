namespace ServiceControl.Audit.Persistence.RavenDb.Transformers
{
    using System.Collections.Generic;
    using System.Linq;
    using Raven.Client.Indexes;

    class MessagesBodyTransformer : AbstractTransformerCreationTask<MessagesBodyTransformer.Input>
    {
        public MessagesBodyTransformer()
        {
            TransformResults = messages => from message in messages
                                           let metadata = message.MessageMetadata
                                           select new
                                           {
                                               MessageId = metadata["MessageId"],
                                               Body = !string.IsNullOrEmpty(message.Body) ? message.Body : metadata["Body"],
                                               BodySize = (int)metadata["ContentLength"],
                                               ContentType = metadata["ContentType"],
                                               BodyNotStored = (bool)metadata["BodyNotStored"]
                                           };
        }

        public class Input
        {
            public string Body { get; set; }
            public Dictionary<string, object> MessageMetadata { get; set; }
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