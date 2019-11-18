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
        static IAcceptanceTestInfrastructureProvider ToHttpExtension(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string instanceName)
        {
            return new AcceptanceTestInfrastructureProvider
            {
                HttpClient = providerMultiInstance.HttpClients[instanceName],
                SerializerSettings = providerMultiInstance.SerializerSettings,
                Port = providerMultiInstance.SettingsPerInstance[instanceName].Port.ToString()
            };
        }

        public static Task Put<T>(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            return providerMultiInstance.ToHttpExtension(instanceName).Put(url, payload, requestHasFailed);
        }

        public static Task<HttpResponseMessage> GetRaw(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string url, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            return providerMultiInstance.ToHttpExtension(instanceName).GetRaw(url);
        }

        public static Task<ManyResult<T>> TryGetMany<T>(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string url, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            return providerMultiInstance.ToHttpExtension(instanceName).TryGetMany(url, condition);
        }

        public static Task<HttpStatusCode> Patch<T>(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string url, T payload = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            return providerMultiInstance.ToHttpExtension(instanceName).Patch(url, payload);
        }

        public static Task<SingleResult<T>> TryGet<T>(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string url, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            return providerMultiInstance.ToHttpExtension(instanceName).TryGet(url, condition);
        }

        public static Task<SingleResult<T>> TryGetSingle<T>(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string url, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            return providerMultiInstance.ToHttpExtension(instanceName).TryGetSingle(url, condition);
        }

        public static Task Post<T>(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            return providerMultiInstance.ToHttpExtension(instanceName).Post(url, payload, requestHasFailed);
        }

    }

    class AcceptanceTestInfrastructureProvider : IAcceptanceTestInfrastructureProvider
    {
        public JsonSerializerSettings SerializerSettings { get; set; }
        public HttpClient HttpClient { get; set; }
        public string Port { get; set; }
    }
}