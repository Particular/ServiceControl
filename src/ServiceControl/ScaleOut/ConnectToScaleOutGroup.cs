namespace ServiceControl.MessageFailures.Api
{
    using System.Collections.Generic;
    using Nancy;
    using Nancy.ModelBinding;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class ConnectToScaleOutGroup : BaseModule
    {
        public ConnectToScaleOutGroup()
        {
            Post["/scaleoutgroups/{id}/connect"] = parameters =>
            {
                string scaleOutGroupId = parameters.id;

                var address = this.Bind<string>();

                if (string.IsNullOrEmpty(address))
                {
                    return HttpStatusCode.BadRequest;
                }

                using (var session = Store.OpenSession())
                {
                    var scaleOutGroup = session.Load<ScaleOutGroup>(scaleOutGroupId) ?? new ScaleOutGroup { Id = scaleOutGroupId };


                    if (!scaleOutGroup.Routes.Contains(address))
                    {
                        scaleOutGroup.Routes.Add(address);

                        session.Store(scaleOutGroup);
                        session.SaveChanges();
                    }

                    return HttpStatusCode.NoContent;
                }

            };
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