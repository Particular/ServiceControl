namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Nancy;
    using Nancy.ModelBinding;
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



        public class EnlistInScaleOutGroup : BaseModule
        {
            public EnlistInScaleOutGroup()
            {
                Post["/scaleoutgroups/{id}/enlist"] = parameters =>
                {
                    string scaleOutGroupId = parameters.id;

                    var request = this.Bind<EnlistRequest>();

                    var address = request.Address;

                    using (var session = Store.OpenSession())
                    {
                        var scaleOutGroup = session.Load<ScaleOutGroup>(scaleOutGroupId) ?? new ScaleOutGroup{Id = scaleOutGroupId};

                     
                        if (!scaleOutGroup.Routes.Contains(address))
                        {
                            scaleOutGroup.Routes.Add(address);

                            session.Store(scaleOutGroup);
                            session.SaveChanges();
                        }

                        return HttpStatusCode.OK;
                    }

                };
            }

        }
    }

    public class EnlistRequest
    {
        public string Address { get; set; }
    }
}
