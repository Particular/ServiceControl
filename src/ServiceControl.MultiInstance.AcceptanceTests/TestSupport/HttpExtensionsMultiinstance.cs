namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using Newtonsoft.Json;

    static class HttpExtensionsMultiInstance
    {
        static IAcceptanceTestInfrastructureProviderSingle ToHttpExtension(this IAcceptanceTestInfrastructureProvider provider, string instanceName)
        {
            return new AcceptanceTestInfrastructureProviderSingle
            {
                HttpClient = provider.HttpClients[instanceName],
                SerializerSettings = provider.SerializerSettings,
                Port = provider.SettingsPerInstance[instanceName].Port.ToString()
            };
        }

        public static Task Put<T>(this IAcceptanceTestInfrastructureProvider provider, string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            return provider.ToHttpExtension(instanceName).Put(url, payload, requestHasFailed);
        }

        public static Task<HttpResponseMessage> GetRaw(this IAcceptanceTestInfrastructureProvider provider, string url, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            return provider.ToHttpExtension(instanceName).GetRaw(url);
        }

        public static Task<ManyResult<T>> TryGetMany<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            return provider.ToHttpExtension(instanceName).TryGetMany(url, condition);
        }

        public static Task<HttpStatusCode> Patch<T>(this IAcceptanceTestInfrastructureProvider provider, string url, T payload = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            return provider.ToHttpExtension(instanceName).Patch(url, payload);
        }

        public static  Task<SingleResult<T>> TryGet<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            return provider.ToHttpExtension(instanceName).TryGet(url, condition);
        }

        public static Task<SingleResult<T>> TryGetSingle<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            return provider.ToHttpExtension(instanceName).TryGetSingle(url, condition);
        }

        public static Task Post<T>(this IAcceptanceTestInfrastructureProvider provider, string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            return provider.ToHttpExtension(instanceName).Post(url, payload, requestHasFailed);
        }

    }

    class AcceptanceTestInfrastructureProviderSingle : IAcceptanceTestInfrastructureProviderSingle
    {
        public JsonSerializerSettings SerializerSettings { get; set; }
        public HttpClient HttpClient { get; set; }
        public string Port { get; set; }
    }
}