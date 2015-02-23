namespace ServiceControl.MessageFailures
{
    using System;
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

        public FailedMessageImporter(IDocumentStore documentStore, IBodyStorage morgue, IBus bus)
        {
            this.documentStore = documentStore;
            this.morgue = morgue;
            this.bus = bus;
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

                failure.ProcessingAttempts.Add(new MessageFailureHistory.ProcessingAttempt
                {
                    ProcessingEndpoint = new EndpointDetails()
                    {
                        Name = message.ProcessedAt.EndpointName,
                        HostId = message.ProcessedAt.HostId
                    },
                    SendingEndpoint = new EndpointDetails()
                    {
                        Name = message.SentFrom.EndpointName,
                        HostId = message.SentFrom.HostId
                    },
                    ContentType = contentType,
                    MessageType = message.MessageType.Name,
                    IsSystemMessage = message.MessageType.IsSystem,
                    TimeSent = ParseSentTime(message.Headers),
                    AttemptedAt = details.TimeOfFailure,
                    FailureDetails = details,
                    MessageId = message.Id,
                    Headers = message.Headers.ToDictionary(),
                    ReplyToAddress = message.Headers.GetOrDefault("NServiceBus.ReplyToAddress"),
                    Recoverable = message.Recoverable,
                    CorrelationId = message.Headers.GetOrDefault("NServiceBus.CorrelationId"),
                    MessageIntent = message.Headers.GetOrDefault("NServiceBus.MessageIntent"),
                    HeadersForSearching = string.Join(",", message.Headers.Select(x => x.Value))
                });

                session.Store(failure);
                session.SaveChanges();

                bus.SendLocal(new ImportFailedMessage()
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

        static DateTime ParseSentTime(HeaderCollection headers)
        {
            string timeSentValue;
            if (headers.TryGet(Headers.TimeSent, out timeSentValue))
            {
                var timeSent = DateTimeExtensions.ToUtcDateTime(timeSentValue);
                return timeSent;
            }
            return DateTime.MinValue;
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