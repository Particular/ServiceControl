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
                    var availableRoutes = session.Load<EndpointRoutes>(endpoint) ?? new EndpointRoutes();

                    return Negotiate.WithModel(availableRoutes.Routes);    
                }
                
            };
        }

    }

}