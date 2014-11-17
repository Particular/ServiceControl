namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using Nancy;
    using Nancy.ModelBinding;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

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
                    var scaleOutGroup = session.Load<ScaleOutGroup>(scaleOutGroupId) ?? new ScaleOutGroup { Id = scaleOutGroupId };


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
        class EnlistRequest
        {
            public string Address { get; set; }
        }
    }

    public class ScaleOutGroup
    {
        public ScaleOutGroup()
        {
            Routes = new List<string>();
        }
        public string Id { get; set; }
        public List<string> Routes { get; set; }
    }
}