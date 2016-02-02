namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using InternalMessages;
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceControl.Recoverability;

    public class RetryMessages : BaseModule
    {
        public RetryMessages()
        {
            Post["/errors/{messageid}/retry", true] = async (parameters, ct) =>
            {
                var failedMessageId = parameters.MessageId;

                if (string.IsNullOrEmpty(failedMessageId))
                {
                    return HttpStatusCode.BadRequest;
                }

                await BusSession.SendLocal<RetryMessage>(m =>
                {
                    m.FailedMessageId = failedMessageId;
                });

                return HttpStatusCode.Accepted;
            };

            Post["/errors/retry", true] = async (parameters, ct) =>
            {
                var ids = this.Bind<List<string>>();

                if (ids.Any(string.IsNullOrEmpty))
                {
                    return HttpStatusCode.BadRequest;
                }

                await BusSession.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = ids.ToArray());

                return HttpStatusCode.Accepted;
            };

            Post["/errors/retry/all", true] = async (parameters, ct) =>
            {
                var request = new RequestRetryAll();

                await BusSession.SendLocal(request);

                return HttpStatusCode.Accepted;
            };

            Post["/errors/{name}/retry/all", true] = async (parameters, ct) =>
            {
                var request = new RequestRetryAll { Endpoint = parameters.name };

                await BusSession.SendLocal(request);

                return HttpStatusCode.Accepted;
            };
        }

        public IBusSession BusSession { get; set; }
    }


}