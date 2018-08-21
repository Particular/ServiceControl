namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Linq;
    using Contracts.Operations;
    using Nancy;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetErrorById : BaseModule
    {
        public GetErrorById()
        {
            Get["/errors/{id}", true] = async (parameters, token) =>
            {
                Guid failedMessageId = parameters.id;

                using (var session = Store.OpenAsyncSession())
                {
                    var message = await session.LoadAsync<FailedMessage>(failedMessageId).ConfigureAwait(false);

                    if (message == null)
                    {
                        return HttpStatusCode.NotFound;
                    }

                    return Negotiate.WithModel(message);
                }
            };

            Get["/errors/last/{id}", true] = async (parameters, token) =>
            {
                Guid failedMessageId = parameters.id;

                using (var session = Store.OpenAsyncSession())
                {
                    var message = await session.LoadAsync<FailedMessage>(failedMessageId).ConfigureAwait(false);

                    if (message == null)
                    {
                        return HttpStatusCode.NotFound;
                    }

                    var result = Map(message, session);

                    return Negotiate.WithModel(result);
                }
            };
        }

        private static FailedMessageView Map(FailedMessage message, IAsyncDocumentSession session)
        {
            var processingAttempt = message.ProcessingAttempts.Last();

            var metadata = processingAttempt.MessageMetadata;
            var failureDetails = processingAttempt.FailureDetails;

            return new FailedMessageView
            {
                Id = message.UniqueMessageId,
                MessageType = metadata.GetAsStringOrNull("MessageType"),
                IsSystemMessage = metadata.GetOrDefault<bool>("IsSystemMessage"),
                SendingEndpoint = metadata.GetOrDefault<EndpointDetails>("SendingEndpoint"),
                ReceivingEndpoint = metadata.GetOrDefault<EndpointDetails>("ReceivingEndpoint"),
                TimeSent = metadata.GetAsNullableDatetime("TimeSent"),
                MessageId = metadata.GetAsStringOrNull("MessageId"),
                Exception = failureDetails.Exception,
                QueueAddress = failureDetails.AddressOfFailingEndpoint,
                NumberOfProcessingAttempts = message.ProcessingAttempts.Count,
                Status = message.Status,
                TimeOfFailure = failureDetails.TimeOfFailure,
                LastModified = session.Advanced.GetMetadataFor(message)["Last-Modified"].Value<DateTime>()
            };
        }
    }
}