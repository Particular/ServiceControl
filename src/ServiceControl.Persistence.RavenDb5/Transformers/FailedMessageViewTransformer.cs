namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Linq;
    using Raven.Client.Documents.Linq;
    using Raven.Client.Documents.Session;
    using ServiceControl.Operations;

    public static class FailedMessageTransformerExtensions
    {
        public static IQueryable<FailedMessageView> ToFailedMessageViews(this IQueryable<FailedMessage> query, IAsyncDocumentSession session)
        {
            var transformed = from failure in query
                              let rec = failure.ProcessingAttempts.Last()
                              let edited = rec.Headers["ServiceControl.EditOf"] != null
                              let metadata = session.Advanced.GetMetadataFor(failure)
                              let lastModified = (DateTime)metadata["@last-modified"]
                              select new FailedMessageView
                              {
                                  Id = failure.UniqueMessageId,
                                  MessageType = (string)rec.MessageMetadata["MessageType"],
                                  IsSystemMessage = (bool)rec.MessageMetadata["IsSystemMessage"],
                                  SendingEndpoint = (EndpointDetails)rec.MessageMetadata["SendingEndpoint"],
                                  ReceivingEndpoint = (EndpointDetails)rec.MessageMetadata["ReceivingEndpoint"],
                                  TimeSent = (DateTime?)rec.MessageMetadata["TimeSent"],
                                  MessageId = (string)rec.MessageMetadata["MessageId"],
                                  Exception = rec.FailureDetails.Exception,
                                  QueueAddress = rec.FailureDetails.AddressOfFailingEndpoint,
                                  NumberOfProcessingAttempts = failure.ProcessingAttempts.Count,
                                  Status = failure.Status,
                                  TimeOfFailure = rec.FailureDetails.TimeOfFailure,
                                  LastModified = lastModified,
                                  Edited = edited,
                                  EditOf = edited ? rec.Headers["ServiceControl.EditOf"] : ""
                              };

            return transformed;
        }
    }
}