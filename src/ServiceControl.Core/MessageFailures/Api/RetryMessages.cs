namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using InternalMessages;
    using Nancy;
    using Nancy.ModelBinding;
    using NServiceBus;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class RetryMessages : BaseModule
    {
        public RetryMessages()
        {
            Post["/errors/{messageid}/retry"] = parameters =>
            {
                string failedMessageId = parameters.MessageId;

                if (string.IsNullOrEmpty(failedMessageId))
                {
                    return HttpStatusCode.BadRequest;
                }

                Bus.SendLocal<RetryMessagesById>(m => m.MessageUniqueIds = new[]
                {
                    failedMessageId
                });

                return HttpStatusCode.Accepted;
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