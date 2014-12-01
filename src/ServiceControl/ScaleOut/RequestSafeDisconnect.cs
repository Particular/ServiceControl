namespace ServiceControl.MessageFailures.Api
{
    using System;
    using System.IO;
    using Nancy;
    using NServiceBus;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Transport;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class RequestSafeDisconnect : BaseModule
    {
        public RequestSafeDisconnect()
        {
            Post["/scaleoutgroups/{id}/requestsafedisconnect"] = parameters =>
            {
                string groupId = parameters.id;
                string address;

                using (var reader = new StreamReader(Request.Body))
                {
                    address = reader.ReadToEnd();
                }



                using (var session = Store.OpenSession())
                {
                    var scaleOutGroup = session.Load<ScaleOutGroupRegistration>(String.Format("ScaleOutGroupRegistrations/{0}/{1}",groupId, address));

                    if (scaleOutGroup == null)
                    {
                        return HttpStatusCode.NoContent;
                    }

                    scaleOutGroup.Status = ScaleOutGroupRegistrationStatus.Disconnecting;

                    session.Store(scaleOutGroup);

                    var transportMessage = ControlMessage.Create(Address.Local);
                    transportMessage.Headers["NServiceBus.DisconnectMessage"] = "true";
                    transportMessage.Headers["ServiceControlCallbackUrl"] = string.Format("{0}/scaleoutgroups/{1}/disconnect",BaseUrl,groupId);

                    SendMessage.Send(transportMessage, Address.Parse(address));
                    
                    session.SaveChanges();
                }

                return HttpStatusCode.NoContent;
            };
        }

        public ISendMessages SendMessage { get; set; }
    }
}