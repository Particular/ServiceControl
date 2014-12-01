namespace ServiceControl.MessageFailures.Api
{
    using System;
    using Nancy;
    using Nancy.Helpers;
    using Nancy.ModelBinding;
    using NServiceBus;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Transport;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;

    public class RequestSafeDisconnect : BaseModule
    {
        public RequestSafeDisconnect()
        {
            Post["/scaleoutgroups/requestsafedisconnect"] = _ =>
            {
                var address = this.Bind<string>();

                var transportMessage = ControlMessage.Create(Address.Local);
                transportMessage.Headers["NServiceBus.DisconnectMessage"] = "true";
                transportMessage.Headers["ServiceControlCallbackUrl"] = BaseUrl + "/SafeToDisconnect/" + HttpUtility.UrlEncode(address);

                SendMessage.Send(transportMessage, Address.Parse(address));

                return HttpStatusCode.NoContent;
            };

            Post["/SafeToDisconnect/{address}"] = parameters =>
            {
                string address = HttpUtility.UrlDecode(parameters.address);
                using (var session = Store.OpenSession())
                {
                    var scaleOutGroup = session.Load<ScaleOutGroupRegistration>(String.Format("ScaleOutGroupRegistrations/*/{0}", address));

                    if (scaleOutGroup == null)
                    {
                        return HttpStatusCode.NoContent;
                    }

                    scaleOutGroup.Status = ScaleOutGroupRegistrationStatus.Disconnecting;

                    session.Store(scaleOutGroup);
                    session.SaveChanges();
                }

                return HttpStatusCode.NoContent;
            };
        }

        public ISendMessages SendMessage { get; set; }
    }
}