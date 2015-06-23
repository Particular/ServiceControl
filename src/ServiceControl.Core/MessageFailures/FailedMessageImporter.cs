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

    class FailedMessageImporter : IProcessFailedMessages
    {
        readonly IBus bus;
        readonly IDocumentStore documentStore;
        readonly IBodyStorage morgue;

        public IEnumerable<IEnrichMessageFailureHistory> MessageFailureEnrichers { get; set; }

        public FailedMessageImporter(IDocumentStore documentStore, IBodyStorage morgue, IBus bus)
        {
            this.documentStore = documentStore;
            this.morgue = morgue;
            this.bus = bus;

            this.MessageFailureEnrichers = Enumerable.Empty<IEnrichMessageFailureHistory>();
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

                foreach(var enricher in MessageFailureEnrichers)
                    enricher.Enrich(failure, message, details);

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