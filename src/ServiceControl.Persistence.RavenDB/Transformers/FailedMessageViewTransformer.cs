namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Linq;
    using Raven.Client.Documents.Linq;
    using Raven.Client.Documents.Queries;

    static class FailedMessageViewTransformer // https://ravendb.net/docs/article-page/4.2/csharp/migration/client-api/session/querying/transformers
    {
        public static IQueryable<FailedMessageView> TransformToFailedMessageView(this IRavenQueryable<FailedMessage> query)
        {
            var failures =
            from failure in query
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
                LastModified = RavenQuery.LastModified(failure),// MetadataFor(failure)["@last-modified"].Value<DateTime>(),
                Edited = edited,
                EditOf = edited ? rec.Headers["ServiceControl.EditOf"] : ""
            };

            return failures.OfType<FailedMessageView>();
        }
    }
}