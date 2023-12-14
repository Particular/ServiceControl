namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Infrastructure.Settings;
    using Microsoft.AspNetCore.Http;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    public record RoutedApiContext(string InstanceId);

    public abstract class RoutedApi<TIn>
        : IApi
        where TIn : RoutedApiContext
    {
        Settings settings;
        IHttpClientFactory httpClientFactory;
        IHttpContextAccessor httpContextAccessor;

        protected RoutedApi(Settings settings, IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
        {
            this.settings = settings;
            this.httpClientFactory = httpClientFactory;
            this.httpContextAccessor = httpContextAccessor;
        }

        public Task<HttpResponseMessage> Execute(TIn input)
        {
            var currentRequest = httpContextAccessor.HttpContext.Request;

            var localInstanceId = settings.InstanceId;

            if (!string.IsNullOrWhiteSpace(input.InstanceId) && input.InstanceId != localInstanceId)
            {
                return RemoteCall(currentRequest, input.InstanceId);
            }

            return LocalQuery(currentRequest, input, localInstanceId);
        }

        // protected virtual string GetInstance(HttpRequestMessage currentRequest, TIn input)
        // {
        //     return currentRequest.GetQueryNameValuePairs().Where(x => x.Key == "instance_id")
        //         .Select(x => x.Value).SingleOrDefault();
        // }

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

                var rawResponse = await httpClient.SendAsync(currentRequest);

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