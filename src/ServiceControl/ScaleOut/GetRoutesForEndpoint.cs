namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
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
