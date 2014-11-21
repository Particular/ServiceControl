namespace ServiceControl.MessageFailures.Api
{
    using Nancy;
    using Nancy.ModelBinding;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class DischargeFromScaleOutGroup : BaseModule
    {
        public DischargeFromScaleOutGroup()
        {
            Delete["/scaleoutgroups/{id}/discharge"] = parameters =>
            {
                string scaleOutGroupId = parameters.id;

                var request = this.Bind<EnlistRequest>();

                var address = request.Address;

                using (var session = Store.OpenSession())
                {
                    var scaleOutGroup = session.Load<ScaleOutGroup>(scaleOutGroupId);

                    if (scaleOutGroup == null)
                    {
                        return HttpStatusCode.OK;
                    }

                    if (scaleOutGroup.Routes.Contains(address))
                    {
                        scaleOutGroup.Routes.Remove(address);

                        session.Store(scaleOutGroup);
                        session.SaveChanges();
                    }

                    return HttpStatusCode.OK;
                }
            };
        }
    }
}