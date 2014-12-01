namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.IO;
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class DisconnectFromScaleOutGroup : BaseModule
    {
        public DisconnectFromScaleOutGroup()
        {
            Post["/scaleoutgroups/{id}/disconnect"] = parameters =>
            {
                string groupId = parameters.id;

                string address;

                using (var reader = new StreamReader(Request.Body))
                {
                    address = reader.ReadToEnd();
                }

                if (string.IsNullOrEmpty(address))
                {
                    return HttpStatusCode.BadRequest;
                }

                using (var session = Store.OpenSession())
                {
                    var endpointInstance = session.Load<ScaleOutGroupRegistration>(String.Format("ScaleOutGroupRegistrations/{0}/{1}", groupId, address));

                    if (endpointInstance == null)
                    {
                        return HttpStatusCode.NoContent;
                    }

                    endpointInstance.Status = ScaleOutGroupRegistrationStatus.Disconnected;
                    session.SaveChanges();

                    return HttpStatusCode.NoContent;
                }
            };
        }
    }
}