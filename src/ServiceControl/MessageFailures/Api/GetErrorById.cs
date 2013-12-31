namespace ServiceControl.MessageFailures.Api
{
    using System;
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetErrorById : BaseModule
    {
        public GetErrorById()
        {
            Get["/errors/{id}"] = parameters =>
            {
                string failedMessageId = parameters.id;

                using (var session = Store.OpenSession())
                {
                    var message = session.Load<FailedMessage>(new Guid(failedMessageId));

                    if (message == null)
                    {
                        return HttpStatusCode.NotFound;
                    }

                    return Negotiate.WithModel(message);
                }
            };
        }

    }

}