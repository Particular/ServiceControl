namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using InternalMessages;
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using Recoverability;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class RetryMessages : BaseModule
    {
        public RetryMessages()
        {
            Post["/errors/{messageid}/retry", true] = async (parameters, token) =>
            {
                var failedMessageId = parameters.MessageId;

                if (string.IsNullOrEmpty(failedMessageId))
                {
                    return HttpStatusCode.BadRequest;
                }

                return await RetryMessagesApi.Execute(this, failedMessageId);
            };

            Post["/errors/retry", true] = async (_, token) =>
            {
                var ids = this.Bind<List<string>>();

                if (ids.Any(string.IsNullOrEmpty))
                {
                    return HttpStatusCode.BadRequest;
                }

                await Bus.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = ids.ToArray())
                    .ConfigureAwait(false);

                return HttpStatusCode.Accepted;
            };

            Post["/errors/queues/{queueaddress}/retry", true] = async (parameters, token) =>
            {
                string queueAddress = parameters.queueaddress;

                if (string.IsNullOrWhiteSpace(queueAddress))
                {
                    return Negotiate.WithReasonPhrase("queueaddress URL parameter must be provided").WithStatusCode(HttpStatusCode.BadRequest);
                }

                await Bus.SendLocal<RetryMessagesByQueueAddress>(m =>
                {
                    m.QueueAddress = queueAddress;
                    m.Status = FailedMessageStatus.Unresolved;
                }).ConfigureAwait(false);

                return HttpStatusCode.Accepted;
            };

            Post["/errors/retry/all", true] = async (_, token) =>
            {
                var request = new RequestRetryAll();

                await Bus.SendLocal(request)
                    .ConfigureAwait(false);

                return HttpStatusCode.Accepted;
            };

            Post["/errors/{name}/retry/all", true] = async (parameters, token) =>
            {
                var request = new RequestRetryAll {Endpoint = parameters.name};

                await Bus.SendLocal(request).ConfigureAwait(false);

                return HttpStatusCode.Accepted;
            };
        }

        public RetryMessagesApi RetryMessagesApi { get; set; }

        public IMessageSession Bus { get; set; }
    }
}