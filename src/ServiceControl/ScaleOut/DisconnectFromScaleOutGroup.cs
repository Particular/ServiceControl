namespace ServiceControl.MessageFailures.Api
{
    using Nancy;
    using Nancy.ModelBinding;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class DisconnectFromScaleOutGroup : BaseModule
    {
        public DisconnectFromScaleOutGroup()
        {
            Delete["/scaleoutgroups/{id}/disconnect"] = parameters =>
            {
                string scaleOutGroupId = parameters.id;

                var address = this.Bind<string>();

                if (string.IsNullOrEmpty(address))
                {
                    return HttpStatusCode.BadRequest;
                }

                using (var session = Store.OpenSession())
                {
                    var scaleOutGroup = session.Load<ScaleOutGroup>(scaleOutGroupId);

                    if (scaleOutGroup == null)
                    {
                        return HttpStatusCode.NoContent;
                    }

                    if (scaleOutGroup.Routes.Contains(address))
                    {
                        scaleOutGroup.Routes.Remove(address);

                        session.Store(scaleOutGroup);
                        session.SaveChanges();
                    }

                    return HttpStatusCode.NoContent;
                }
            };
        }
    }
}