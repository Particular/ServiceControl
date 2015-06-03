namespace Particular.Backend.Debugging.RavenDB.Api
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class MessagesBodyTransformer : AbstractTransformerCreationTask<MessageSnapshot>
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
                select new Result
                {
                    MessageId = message.MessageId,
                    Body = message.Body != null ? message.Body.Text : null,
                    BodySize = message.Body != null ? message.Body.ContentLength : 0,
                    ContentType = message.Body != null ? message.Body.ContentType : null,
                };
        }
    }
}