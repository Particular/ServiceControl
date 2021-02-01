namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.Settings;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    abstract class RoutedApi<TIn> : IApi
    {
        public Settings Settings { get; set; }
        public Func<HttpClient> HttpClientFactory { get; set; }

        public Task<HttpResponseMessage> Execute(ApiController controller, TIn input)
        {
            var currentRequest = controller.Request;

            var instanceId = GetInstance(currentRequest, input);

            var localInstanceId = InstanceIdGenerator.FromApiUrl(Settings.ApiUrl);

            if (!string.IsNullOrWhiteSpace(instanceId) && instanceId != localInstanceId)
            {
                return RemoteCall(currentRequest, instanceId);
            }

            return LocalQuery(currentRequest, input, localInstanceId);
        }

        protected virtual string GetInstance(HttpRequestMessage currentRequest, TIn input)
        {
            return currentRequest.GetQueryNameValuePairs().Where(x => x.Key == "instance_id")
                .Select(x => x.Value).SingleOrDefault();
        }

        protected abstract Task<HttpResponseMessage> LocalQuery(HttpRequestMessage request, TIn input, string instanceId);

        async Task<HttpResponseMessage> RemoteCall(HttpRequestMessage currentRequest, string instanceId)
        {
            var remoteUri = InstanceIdGenerator.ToApiUri(instanceId);

            var instanceUri = currentRequest.RedirectToRemoteUri(remoteUri);

            var httpClient = HttpClientFactory();
            try
            {
                currentRequest.RequestUri = instanceUri;
                if (currentRequest.Method == HttpMethod.Get)
                {
                    currentRequest.Content = null;
                }

                currentRequest.Headers.Host = remoteUri.Authority; //switch the host header to the new instance host

                var rawResponse = await httpClient.SendAsync(currentRequest).ConfigureAwait(false);

                return rawResponse;
            }
            catch (Exception exception)
            {
                logger.Warn($"Failed to query remote instance at {remoteUri}.", exception);

                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        static ILog logger = LogManager.GetLogger(typeof(RoutedApi<TIn>));
    }
}