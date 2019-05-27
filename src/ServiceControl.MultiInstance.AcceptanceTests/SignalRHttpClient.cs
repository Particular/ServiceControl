namespace ServiceBus.Management.AcceptanceTests
{
    using System.Net.Http;
    using Microsoft.AspNet.SignalR.Client.Http;

    public class SignalRHttpClient : DefaultHttpClient
    {
        public SignalRHttpClient(HttpMessageHandler handler)
        {
            this.handler = handler;
        }

        protected override HttpMessageHandler CreateHandler()
        {
            return handler;
        }

        private readonly HttpMessageHandler handler;
    }
}