namespace ServiceControl.MessageFailures.Api
{
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
                var request = this.Bind<EnlistRequest>();

                var transportMessage = ControlMessage.Create(Address.Local);
                transportMessage.Headers["NServiceBus.DisconnectMessage"] = "true";
                transportMessage.Headers["ServiceControlCallbackUrl"] = BaseUrl + "/SafeToDisconnect/" + HttpUtility.UrlEncode(request.Address);

                SendMessage.Send(transportMessage, Address.Parse(request.Address));

                return HttpStatusCode.OK;
            };

            Post["/SafeToDisconnect/{address}"] = parameters =>
            {
                string address = HttpUtility.UrlDecode(parameters.address);

                // What to do here?
                
                return HttpStatusCode.OK;
            };
        }

        public ISendMessages SendMessage { get; set; }
    }
}