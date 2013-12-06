namespace ServiceControl.MessageFailures.Api
{
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetErrorById : BaseModule
    {

        public GetErrorById()
        {
            Get["/errors/{id}"] = parameters =>
            {
                string messageId = parameters.id;

                using (var session = Store.OpenSession())
                {
                    var message = session.Load<FailedMessage>(messageId);

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