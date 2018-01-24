namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.Linq;
    using Nancy;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Contracts.Operations;

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

             return new FailedMessageView
             {
                 Id = message.UniqueMessageId,
                 MessageType = processingAttempt.MessageMetadata["MessageType"].ToString(),
                 IsSystemMessage = (bool)processingAttempt.MessageMetadata["IsSystemMessage"],
                 SendingEndpoint = (EndpointDetails)processingAttempt.MessageMetadata["SendingEndpoint"],
                 ReceivingEndpoint = (EndpointDetails)processingAttempt.MessageMetadata["ReceivingEndpoint"],
                 TimeSent = DateTime.Parse(processingAttempt.MessageMetadata["TimeSent"].ToString()),
                 MessageId = processingAttempt.MessageMetadata["MessageId"].ToString(),
                 Exception = processingAttempt.FailureDetails.Exception,
                 QueueAddress = processingAttempt.FailureDetails.AddressOfFailingEndpoint,
                 NumberOfProcessingAttempts = message.ProcessingAttempts.Count,
                 Status = message.Status,
                 TimeOfFailure = processingAttempt.FailureDetails.TimeOfFailure,
                 LastModified = session.Advanced.GetMetadataFor(message)["Last-Modified"].Value<DateTime>()
             };
         }
    }

}