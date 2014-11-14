namespace ServiceControl.MessageFailures.Api
{
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetRoutesForEndpoint : BaseModule
    {
        public GetRoutesForEndpoint()
        {
            Get["/routes/{id}"] = parameters =>
            {
                string endpoint = parameters.id;

                using(var session = Store.OpenSession())
                {
                    var availableRoutes = session.Load<EndpointRoutes>(endpoint);

                    if (availableRoutes == null)
                    {
                        return HttpStatusCode.NotFound;
                    }

                    return Negotiate.WithModel(availableRoutes.Routes);    
                }
                
            };
        }

    }

}