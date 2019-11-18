namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Newtonsoft.Json;

    static class HttpExtensions
    {
        public static async Task Put<T>(this IAcceptanceTestInfrastructureProviderSingle provider, string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.Port}{url}";
            }

            if (requestHasFailed == null)
            {
                requestHasFailed = statusCode => statusCode != HttpStatusCode.OK && statusCode != HttpStatusCode.Accepted;
            }

            var json = JsonConvert.SerializeObject(payload, provider.SerializerSettings);
            var httpClient = provider.HttpClient;
            var response = await httpClient.PutAsync(url, new StringContent(json, null, "application/json"));

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");

            if (requestHasFailed(response.StatusCode))
            {
                throw new Exception($"Expected status code not received, instead got {response.StatusCode}.");
            }
        }

        public static Task<HttpResponseMessage> GetRaw(this IAcceptanceTestInfrastructureProviderSingle provider, string url)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.Port}{url}";
            }

            var httpClient = provider.HttpClient;
            return httpClient.GetAsync(url);
        }

        public static async Task<ManyResult<T>> TryGetMany<T>(this IAcceptanceTestInfrastructureProviderSingle provider, string url, Predicate<T> condition = null) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            var response = await provider.GetInternal<List<T>>(url).ConfigureAwait(false);

            if (response == null || !response.Any(m => condition(m)))
            {
                return ManyResult<T>.Empty;
            }

            return ManyResult<T>.New(true, response);
        }

        public static async Task<HttpStatusCode> Patch<T>(this IAcceptanceTestInfrastructureProviderSingle provider, string url, T payload = null) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.Port}{url}";
            }

            var json = JsonConvert.SerializeObject(payload, provider.SerializerSettings);
            var httpClient = provider.HttpClient;
            var response = await httpClient.PatchAsync(url, new StringContent(json, null, "application/json")).ConfigureAwait(false);

            Console.WriteLine($"PATCH - {url} - {(int)response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"Call failed: {(int)response.StatusCode} - {response.ReasonPhrase} - {body}");
            }

            return response.StatusCode;
        }

        public static async Task<SingleResult<T>> TryGet<T>(this IAcceptanceTestInfrastructureProviderSingle provider, string url, Predicate<T> condition = null) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            var response = await provider.GetInternal<T>(url).ConfigureAwait(false);

            if (response == null || !condition(response))
            {
                return SingleResult<T>.Empty;
            }

            return SingleResult<T>.New(response);
        }

        public static async Task<SingleResult<T>> TryGet<T>(this IAcceptanceTestInfrastructureProviderSingle provider, string url, Func<T, Task<bool>> condition) where T : class
        {
            var response = await provider.GetInternal<T>(url).ConfigureAwait(false);

            if (response == null || !await condition(response).ConfigureAwait(false))
            {
                return SingleResult<T>.Empty;
            }

            return SingleResult<T>.New(response);
        }

        public static async Task<SingleResult<T>> TryGetSingle<T>(this IAcceptanceTestInfrastructureProviderSingle provider, string url, Predicate<T> condition = null) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            var response = await provider.GetInternal<List<T>>(url);
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
                return SingleResult<T>.New(item);
            }

            return SingleResult<T>.Empty;
        }

        public static async Task<HttpStatusCode> Get(this IAcceptanceTestInfrastructureProviderSingle provider, string url)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.Port}{url}";
            }

            var httpClient = provider.HttpClient;
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");

            return response.StatusCode;
        }

        public static async Task Post<T>(this IAcceptanceTestInfrastructureProviderSingle provider, string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.Port}{url}";
            }

            var json = JsonConvert.SerializeObject(payload, provider.SerializerSettings);
            var httpClient = provider.HttpClient;
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

        public static async Task Delete(this IAcceptanceTestInfrastructureProviderSingle provider, string url)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.Port}{url}";
            }

            var httpClient = provider.HttpClient;
            var response = await httpClient.DeleteAsync(url);

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Call failed: {(int)response.StatusCode} - {response.ReasonPhrase} - {body}");
            }
        }

        public static async Task<byte[]> DownloadData(this IAcceptanceTestInfrastructureProviderSingle provider, string url, HttpStatusCode successCode = HttpStatusCode.OK)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.Port}/api{url}";
            }

            var httpClient = provider.HttpClient;
            var response = await httpClient.GetAsync(url);
            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");
            if (response.StatusCode != successCode)
            {
                throw new Exception($"Expected status code of {successCode}, but instead got {response.StatusCode}.");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        static async Task<T> GetInternal<T>(this IAcceptanceTestInfrastructureProviderSingle provider, string url) where T : class
        {
            var response = await provider.GetRaw(url).ConfigureAwait(false);

            //for now
            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                LogRequest();
                return null;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                LogRequest(response.ReasonPhrase + content);
                throw new InvalidOperationException($"Call failed: {(int)response.StatusCode} - {response.ReasonPhrase} {Environment.NewLine} {content}");
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

    interface IAcceptanceTestInfrastructureProviderSingle
    {
        JsonSerializerSettings SerializerSettings { get; set; }
        HttpClient HttpClient { get; set; }
        string Port { get; set; }
    }
}