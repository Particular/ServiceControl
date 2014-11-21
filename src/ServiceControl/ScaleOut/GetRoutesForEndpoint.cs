namespace ServiceControl.MessageFailures.Api
{
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class GetRoutesForScaleOutGroup : BaseModule
    {
        public GetRoutesForScaleOutGroup()
        {
            Get["/routes/{id}"] = parameters =>
            {
                string endpoint = parameters.id;

                using (var session = Store.OpenSession())
                {
                    var availableRoutes = session.Load<ScaleOutGroup>(endpoint);

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
