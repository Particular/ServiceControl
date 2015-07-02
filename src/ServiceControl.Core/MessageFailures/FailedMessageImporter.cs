namespace ServiceControl.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Contracts.Operations;
    using NServiceBus;
    using Particular.Operations.Ingestion.Api;
    using Raven.Client;
    using ServiceControl.MessageFailures.InternalMessages;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Recoverability.Groups;

    class FailedMessageImporter : IProcessFailedMessages
    {
        readonly IBus bus;
        readonly ProcessingAttemptMessageFailureHistoryEnricher processingAttemptEnricher;
        readonly MessageFailureHistoryGrouper grouper;
        readonly IDocumentStore documentStore;
        readonly IBodyStorage morgue;
        
        public FailedMessageImporter(IDocumentStore documentStore, IBodyStorage morgue, IBus bus, ProcessingAttemptMessageFailureHistoryEnricher processingAttemptEnricher, MessageFailureHistoryGrouper grouper)
        {
            this.documentStore = documentStore;
            this.morgue = morgue;
            this.bus = bus;
            this.processingAttemptEnricher = processingAttemptEnricher;
            this.grouper = grouper;
        }

        public void ProcessFailed(IngestedMessage message)
        {
            using (var session = documentStore.OpenSession())
            {
                var contentType = message.Headers.GetOrDefault("NServiceBus.ContentType", "text/xml");
                StoreBody(message, contentType);

                var documentId = MessageFailureHistory.MakeDocumentId(message.UniqueId);

                var failure = session.Load<MessageFailureHistory>(documentId) ?? new MessageFailureHistory
                {
                    Id = documentId,
                    UniqueMessageId = message.UniqueId
                };

                failure.Status = FailedMessageStatus.Unresolved;

                var details = ParseFailureDetails(message.Headers);

                //check for duplicate
                if (failure.ProcessingAttempts.Any(a => a.AttemptedAt == details.TimeOfFailure))
                {
                    return;
                }

                processingAttemptEnricher.Enrich(failure, message, details);
                grouper.Group(failure);
                
                session.Store(failure);
                session.SaveChanges();

                bus.SendLocal(new ImportFailedMessage
                {
                    FailingEndpointName = message.ProcessedAt.EndpointName,
                    FailureDetails = details,
                    MessageType = message.MessageType.Name,
                    UniqueMessageId = message.UniqueId,
                    RetryId = message.Headers.GetOrDefault("ServiceControl.RetryId"),
                });
            }
        }

        public void StoreBody(IngestedMessage message, string contentType)
        {
            using (var bodyStream = new MemoryStream(message.Body))
            {
                morgue.Store(message.Id, contentType, message.BodyLength, bodyStream);
            }
        }

        static DateTime ParseTimeOfFailure(HeaderCollection headers)
        {
            var timeOfFailure = new DateTime();
            string timeOfFailureString;
            if (headers.TryGet("NServiceBus.TimeOfFailure", out timeOfFailureString))
            {
                timeOfFailure = DateTimeExtensions.ToUtcDateTime(timeOfFailureString);
            }
            return timeOfFailure;
        }

        FailureDetails ParseFailureDetails(HeaderCollection headers)
        {
            var result = new FailureDetails
            {
                TimeOfFailure = ParseTimeOfFailure(headers),
                Exception = GetException(headers),
                AddressOfFailingEndpoint = headers.GetOrDefault("NServiceBus.FailedQ")
            };
            return result;
        }

        ExceptionDetails GetException(HeaderCollection headers)
        {
            var exceptionDetails = new ExceptionDetails
            {
                ExceptionType = headers.GetOrDefault("NServiceBus.ExceptionInfo.ExceptionType"),
                Message = headers.GetOrDefault("NServiceBus.ExceptionInfo.Message"),
                Source = headers.GetOrDefault("NServiceBus.ExceptionInfo.Source"),
                StackTrace = headers.GetOrDefault("NServiceBus.ExceptionInfo.StackTrace")
            };

            return exceptionDetails;
        }
    }
}