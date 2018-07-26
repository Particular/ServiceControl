namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using Newtonsoft.Json;

    public static class HttpExtensions
    {
        public static async Task Put<T>(this IAcceptanceTestInfrastructureProvider provider, string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.SettingsPerInstance[instanceName].Port}{url}";
            }

            if (requestHasFailed == null)
            {
                requestHasFailed = statusCode => statusCode != HttpStatusCode.OK && statusCode != HttpStatusCode.Accepted;
            }

            var json = JsonConvert.SerializeObject(payload, provider.SerializerSettings);
            var httpClient = provider.HttpClients[instanceName];
            var response = await httpClient.PutAsync(url, new StringContent(json, null, "application/json"));

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");

            if (requestHasFailed(response.StatusCode))
            {
                throw new Exception($"Expected status code not received, instead got {response.StatusCode}.");
            }
        }

        public static Task<HttpResponseMessage> GetRaw(this IAcceptanceTestInfrastructureProvider provider, string url, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.SettingsPerInstance[instanceName].Port}{url}";
            }

            var httpClient = provider.HttpClients[instanceName];
            return httpClient.GetAsync(url);
        }

        public static async Task<ManyResult<T>> TryGetMany<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            var response = await provider.GetInternal<List<T>>(url, instanceName).ConfigureAwait(false);

            if (response == null || !response.Any(m => condition(m)))
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return ManyResult<T>.Empty;
            }

            return ManyResult<T>.New(true, response);
        }

        public static async Task<HttpStatusCode> Patch<T>(this IAcceptanceTestInfrastructureProvider provider, string url, T payload = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.SettingsPerInstance[instanceName].Port}{url}";
            }

            var json = JsonConvert.SerializeObject(payload, provider.SerializerSettings);
            var httpClient = provider.HttpClients[instanceName];
            var response = await httpClient.PatchAsync(url, new StringContent(json, null, "application/json")).ConfigureAwait(false);

            Console.WriteLine($"PATCH - {url} - {(int)response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"Call failed: {(int)response.StatusCode} - {response.ReasonPhrase} - {body}");
            }

            return response.StatusCode;
        }

        public static async Task<SingleResult<T>> TryGet<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            var response = await provider.GetInternal<T>(url, instanceName).ConfigureAwait(false);

            if (response == null || !condition(response))
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return SingleResult<T>.Empty;
            }

            return SingleResult<T>.New(response);
        }

        public static async Task<SingleResult<T>> TryGet<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Func<T, Task<bool>> condition, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            var response = await provider.GetInternal<T>(url, instanceName).ConfigureAwait(false);

            if (response == null || !await condition(response).ConfigureAwait(false))
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return SingleResult<T>.Empty;
            }

            return SingleResult<T>.New(response);
        }

        public static async Task<SingleResult<T>> TryGetSingle<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            var response = await provider.GetInternal<List<T>>(url, instanceName);
            T item = null;
            if (response != null)
            {
                var items = response.Where(i => condition(i)).ToList();

                if (items.Count > 1)
                {
                    throw new InvalidOperationException("More than one matching element found");
                }

                item = items.SingleOrDefault();
            }

            if (item != null)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return SingleResult<T>.New(item);
            }

            return SingleResult<T>.Empty;
        }

        public static async Task<HttpStatusCode> Get(this IAcceptanceTestInfrastructureProvider provider, string url, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.SettingsPerInstance[instanceName].Port}{url}";
            }

            var httpClient = provider.HttpClients[instanceName];
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");

            return response.StatusCode;
        }

        public static async Task Post<T>(this IAcceptanceTestInfrastructureProvider provider, string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.SettingsPerInstance[instanceName].Port}{url}";
            }

            var json = JsonConvert.SerializeObject(payload, provider.SerializerSettings);
            var httpClient = provider.HttpClients[instanceName];
            var response = await httpClient.PostAsync(url, new StringContent(json, null, "application/json")).ConfigureAwait(false);

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");

            if (requestHasFailed != null)
            {
                if (requestHasFailed(response.StatusCode))
                {
                    throw new Exception($"Expected status code not received, instead got {response.StatusCode}.");
                }

                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"Call failed: {(int)response.StatusCode} - {response.ReasonPhrase} - {body}");
            }
        }

        public static async Task Delete(this IAcceptanceTestInfrastructureProvider provider, string url, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.SettingsPerInstance[instanceName].Port}{url}";
            }

            var httpClient = provider.HttpClients[instanceName];
            var response = await httpClient.DeleteAsync(url);

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Call failed: {(int)response.StatusCode} - {response.ReasonPhrase} - {body}");
            }
        }

        public static async Task<byte[]> DownloadData(this IAcceptanceTestInfrastructureProvider provider, string url, HttpStatusCode successCode = HttpStatusCode.OK, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.SettingsPerInstance[instanceName].Port}/api{url}";
            }

            var httpClient = provider.HttpClients[instanceName];
            var response = await httpClient.GetAsync(url);
            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");
            if (response.StatusCode != successCode)
            {
                throw new Exception($"Expected status code of {successCode}, but instead got {response.StatusCode}.");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        static async Task<T> GetInternal<T>(this IAcceptanceTestInfrastructureProvider provider, string url, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            var response = await provider.GetRaw(url, instanceName).ConfigureAwait(false);

            //for now
            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                LogRequest();
                await Task.Delay(1000).ConfigureAwait(false);
                return null;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                LogRequest(response.ReasonPhrase);
                throw new InvalidOperationException($"Call failed: {(int)response.StatusCode} - {response.ReasonPhrase}");
            }

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            LogRequest();
            return JsonConvert.DeserializeObject<T>(body, provider.SerializerSettings);

            void LogRequest(string additionalInfo = null)
            {
                var additionalInfoString = additionalInfo != null ? ": " + additionalInfo : string.Empty;
                Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}{additionalInfoString}");
            }
        }
    }
}