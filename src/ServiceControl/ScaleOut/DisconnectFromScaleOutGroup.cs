namespace ServiceControl.MessageFailures.Api
{
    using System;
    using Nancy;
    using Nancy.ModelBinding;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class DisconnectFromScaleOutGroup : BaseModule
    {
        public DisconnectFromScaleOutGroup()
        {
            Delete["/scaleoutgroups/{id}/disconnect"] = parameters =>
            {
                string groupId = parameters.id;

                var address = this.Bind<string>();

                if (string.IsNullOrEmpty(address))
                {
                    return HttpStatusCode.BadRequest;
                }

                using (var session = Store.OpenSession())
                {
                    var scaleOutGroup = session.Load<ScaleOutGroupRegistration>(String.Format("ScaleOutGroupRegistrations/{0}/{1}", groupId, address));

                    if (scaleOutGroup == null)
                    {
                        return HttpStatusCode.NoContent;
                    }

                    //TODO: Should we validate that the status is at Disconnecting ?
                    session.Delete(scaleOutGroup);
                    session.SaveChanges();

                    return HttpStatusCode.NoContent;
                }
            };
        }
    }
}