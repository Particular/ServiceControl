namespace ServiceControl.MessageFailures.Api
{
    using System.IO;
    using Nancy;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class ConnectToScaleOutGroup : BaseModule
    {
        public ConnectToScaleOutGroup()
        {
            Post["/scaleoutgroups/{id}/connect"] = parameters =>
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
                    var scaleOutGroupRegistration = new ScaleOutGroupRegistration(groupId, address);
                   
                    session.Store(scaleOutGroupRegistration);
                    session.SaveChanges();

                    return HttpStatusCode.NoContent;
                }
            };
        }
    }
}