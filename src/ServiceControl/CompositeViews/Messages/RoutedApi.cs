namespace ServiceControl.CompositeViews.Messages
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.Net.Http.Headers;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    public abstract record RoutedApiContext(string InstanceId);

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
            var currentRequest = httpContextAccessor.HttpContext!.Request;
            var pathAndQuery = httpContextAccessor.HttpContext!.Request.GetEncodedPathAndQuery();

            if (!string.IsNullOrWhiteSpace(input.InstanceId) && input.InstanceId != settings.InstanceId)
            {
                return RemoteCall(currentRequest, pathAndQuery, input.InstanceId);
            }

            return LocalQuery(input);
        }

        protected abstract Task<HttpResponseMessage> LocalQuery(TIn input);

        async Task<HttpResponseMessage> RemoteCall(HttpRequest currentRequest, string pathAndQuery, string instanceId)
        {
            var httpClient = httpClientFactory.CreateClient(instanceId);
            try
            {
                var httpRequestMessage = new HttpRequestMessage(new HttpMethod(currentRequest.Method), pathAndQuery);
                // We need to forward all the incoming headers
                foreach (var currentRequestHeader in currentRequest.Headers)
                {
                    // TODO double check this is needed
                    if (currentRequestHeader.Key == HeaderNames.Host)
                    {
                        continue;
                    }
                    httpRequestMessage.Headers.Add(currentRequestHeader.Key, currentRequestHeader.Value.ToString());
                }

                if (currentRequest.Method != HttpMethod.Get.ToString() && currentRequest.Method != HttpMethod.Head.ToString())
                {
                    httpRequestMessage.Content = new StreamContent(currentRequest.Body);
                }
                var rawResponse = await httpClient.SendAsync(httpRequestMessage);

                return rawResponse;
            }
            catch (Exception exception)
            {
                logger.Warn($"Failed to query remote instance at {httpClient.BaseAddress + pathAndQuery}.", exception);

                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        static ILog logger = LogManager.GetLogger(typeof(RoutedApi<TIn>));
    }
}