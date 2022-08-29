namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Contracts.Operations;
    using Infrastructure.WebApi;
    using Raven.Client;

    class GetErrorByIdController : ApiController
    {
        public GetErrorByIdController(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        [Route("errors/{failedmessageid:guid}")]
        [HttpGet]
        public async Task<HttpResponseMessage> ErrorBy(Guid failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var message = await session.LoadAsync<FailedMessage>(failedMessageId).ConfigureAwait(false);

                if (message == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound);
                }

                return Negotiator.FromModel(Request, message);
            }
        }

        [Route("errors/last/{failedmessageid:guid}")]
        [HttpGet]
        public async Task<HttpResponseMessage> ErrorLastBy(Guid failedMessageId)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                var message = await session.LoadAsync<FailedMessage>(failedMessageId).ConfigureAwait(false);

                if (message == null)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound);
                }

                var result = Map(message, session);

                return Negotiator.FromModel(Request, result);
            }
        }

        static FailedMessageView Map(FailedMessage message, IAsyncDocumentSession session)
        {
            var processingAttempt = message.ProcessingAttempts.Last();

            var metadata = processingAttempt.MessageMetadata;
            var failureDetails = processingAttempt.FailureDetails;
            var wasEdited = message.ProcessingAttempts.Last().Headers.ContainsKey("ServiceControl.EditOf");

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
                LastModified = session.Advanced.GetMetadataFor(message)["Last-Modified"].Value<DateTime>(),
                Edited = wasEdited,
                EditOf = wasEdited ? message.ProcessingAttempts.Last().Headers["ServiceControl.EditOf"] : ""
            };
        }

        readonly IDocumentStore documentStore;
    }
}