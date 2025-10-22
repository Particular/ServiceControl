namespace ServiceControl.MultiInstance.AcceptanceTests.TestSupport
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using ServiceBus.Management.Infrastructure.Settings;

    static class HttpExtensionsMultiInstance
    {
        static IAcceptanceTestInfrastructureProvider ToHttpExtension(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string instanceName) =>
            new AcceptanceTestInfrastructureProvider
            {
                HttpClient = providerMultiInstance.HttpClients[instanceName],
                SerializerOptions = providerMultiInstance.SerializerOptions[instanceName],
            };

        public static Task Put<T>(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null, string instanceName = PrimaryOptions.DEFAULT_INSTANCE_NAME) where T : class
        {
            return providerMultiInstance.ToHttpExtension(instanceName).Put(url, payload, requestHasFailed);
        }

        public static Task<HttpResponseMessage> GetRaw(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string url, string instanceName = PrimaryOptions.DEFAULT_INSTANCE_NAME)
        {
            return providerMultiInstance.ToHttpExtension(instanceName).GetRaw(url);
        }

        public static Task<ManyResult<T>> TryGetMany<T>(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string url, Predicate<T> condition = null, string instanceName = PrimaryOptions.DEFAULT_INSTANCE_NAME) where T : class
        {
            return providerMultiInstance.ToHttpExtension(instanceName).TryGetMany(url, condition);
        }

        public static Task<HttpStatusCode> Patch<T>(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string url, T payload = null, string instanceName = PrimaryOptions.DEFAULT_INSTANCE_NAME) where T : class
        {
            return providerMultiInstance.ToHttpExtension(instanceName).Patch(url, payload);
        }

        public static Task<SingleResult<T>> TryGet<T>(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string url, Predicate<T> condition = null, string instanceName = PrimaryOptions.DEFAULT_INSTANCE_NAME) where T : class
        {
            return providerMultiInstance.ToHttpExtension(instanceName).TryGet(url, condition);
        }

        public static Task<SingleResult<T>> TryGetSingle<T>(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string url, Predicate<T> condition = null, string instanceName = PrimaryOptions.DEFAULT_INSTANCE_NAME) where T : class
        {
            return providerMultiInstance.ToHttpExtension(instanceName).TryGetSingle(url, condition);
        }

        public static Task Post<T>(this IAcceptanceTestInfrastructureProviderMultiInstance providerMultiInstance, string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null, string instanceName = PrimaryOptions.DEFAULT_INSTANCE_NAME) where T : class
        {
            return providerMultiInstance.ToHttpExtension(instanceName).Post(url, payload, requestHasFailed);
        }

    }

    class AcceptanceTestInfrastructureProvider : IAcceptanceTestInfrastructureProvider
    {
        public HttpClient HttpClient { get; set; }
        public JsonSerializerOptions SerializerOptions { get; set; }
    }
}