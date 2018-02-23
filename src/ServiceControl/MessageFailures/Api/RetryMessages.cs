namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using InternalMessages;
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Recoverability;

    public class RetryMessages : BaseModule
    {
        private static ILog Logger = LogManager.GetLogger<RetryMessages>();

        public RetryMessagesApi RetryMessagesApi { get; set; }

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

            Post["/errors/retry"] = _ =>
            {
                var ids = this.Bind<List<string>>();

                if (ids.Any(string.IsNullOrEmpty))
                {
                    return HttpStatusCode.BadRequest;
                }

                Bus.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = ids.ToArray());

                return HttpStatusCode.Accepted;
            };

            Post["/errors/queues/{queueaddress}/retry"] = parameters =>
            {
                string queueAddress = parameters.queueaddress;

                if (string.IsNullOrWhiteSpace(queueAddress))
                {
                    return Negotiate.WithReasonPhrase("queueaddress URL parameter must be provided").WithStatusCode(HttpStatusCode.BadRequest);
                }

                Bus.SendLocal<RetryMessagesByQueueAddress>(m =>
                {
                    m.QueueAddress = queueAddress;
                    m.Status = FailedMessageStatus.Unresolved;
                });

                return HttpStatusCode.Accepted;
            };

            Post["/errors/retry/all"] = _ =>
            {
                var request = new RequestRetryAll();

                Bus.SendLocal(request);

                return HttpStatusCode.Accepted;
            };

            Post["/errors/{name}/retry/all"] = parameters =>
            {
                var request = new RequestRetryAll { Endpoint = parameters.name };

                Bus.SendLocal(request);

                return HttpStatusCode.Accepted;
            };
        }

        public IBus Bus { get; set; }
    }


}