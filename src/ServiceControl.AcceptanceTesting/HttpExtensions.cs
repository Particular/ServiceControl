namespace ServiceControl.AcceptanceTesting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading;
    using System.Threading.Tasks;

    public static class HttpExtensions
    {
        public static async Task Put<T>(this IAcceptanceTestInfrastructureProvider provider, string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null, CancellationToken cancellationToken = default) where T : class
        {
            requestHasFailed ??= statusCode => statusCode is not HttpStatusCode.OK and not HttpStatusCode.Accepted;

            var httpClient = provider.HttpClient;
            var response = await httpClient.PutAsJsonAsync(url, payload, provider.SerializerOptions, cancellationToken);

            if (requestHasFailed(response.StatusCode))
            {
                throw new Exception($"Expected status code not received, instead got {response.StatusCode}.");
            }
        }

        public static Task<HttpResponseMessage> GetRaw(this IAcceptanceTestInfrastructureProvider provider, string url, CancellationToken cancellationToken = default)
        {
            var httpClient = provider.HttpClient;
            return httpClient.GetAsync(url, cancellationToken);
        }

        public static Task<HttpResponseMessage> Options(this IAcceptanceTestInfrastructureProvider provider, string url, CancellationToken cancellationToken = default)
        {
            var httpClient = provider.HttpClient;
            var request = new HttpRequestMessage(HttpMethod.Options, url);
            return httpClient.SendAsync(request, cancellationToken);
        }

        public static async Task<ManyResult<T>> TryGetMany<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Predicate<T> condition = null, CancellationToken cancellationToken = default) where T : class
        {
            condition ??= _ => true;

            var response = await provider.GetInternal<List<T>>(url, cancellationToken);

            if (response == null || !response.Any(m => condition(m)))
            {
                return ManyResult<T>.Empty;
            }

            return ManyResult<T>.New(true, response.Where(m => condition(m)).ToList());
        }

        public static async Task<HttpStatusCode> Patch<T>(this IAcceptanceTestInfrastructureProvider provider, string url, T payload = null, CancellationToken cancellationToken = default) where T : class
        {
            var httpClient = provider.HttpClient;
            var response = await httpClient.PatchAsJsonAsync(url, payload, provider.SerializerOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Call failed: {(int)response.StatusCode} - {response.ReasonPhrase} - {body}");
            }

            return response.StatusCode;
        }

        public static async Task<SingleResult<T>> TryGet<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Predicate<T> condition = null, CancellationToken cancellationToken = default) where T : class
        {
            condition ??= _ => true;

            var response = await provider.GetInternal<T>(url, cancellationToken);

            if (response == null || !condition(response))
            {
                return SingleResult<T>.Empty;
            }

            return SingleResult<T>.New(response);
        }

        public static async Task<SingleResult<T>> TryGet<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Func<T, CancellationToken, Task<bool>> condition, CancellationToken cancellationToken = default) where T : class
        {
            var response = await provider.GetInternal<T>(url, cancellationToken);

            if (response == null || !await condition(response, cancellationToken))
            {
                return SingleResult<T>.Empty;
            }

            return SingleResult<T>.New(response);
        }

        public static async Task<SingleResult<T>> TryGetSingle<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Predicate<T> condition = null, CancellationToken cancellationToken = default) where T : class
        {
            condition ??= _ => true;

            var response = await provider.GetInternal<List<T>>(url, cancellationToken);
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

        public static async Task<HttpStatusCode> Get(this IAcceptanceTestInfrastructureProvider provider, string url, CancellationToken cancellationToken = default)
        {
            var httpClient = provider.HttpClient;
            var response = await httpClient.GetAsync(url, cancellationToken);
            return response.StatusCode;
        }

        public static async Task Post<T>(this IAcceptanceTestInfrastructureProvider provider, string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null, CancellationToken cancellationToken = default) where T : class
        {
            var httpClient = provider.HttpClient;
            var response = await httpClient.PostAsJsonAsync(url, payload, provider.SerializerOptions, cancellationToken);

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
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Call failed: {(int)response.StatusCode} - {response.ReasonPhrase} - {body}");
            }
        }

        public static async Task Delete(this IAcceptanceTestInfrastructureProvider provider, string url, CancellationToken cancellationToken = default)
        {
            var httpClient = provider.HttpClient;
            var response = await httpClient.DeleteAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException($"Call failed: {(int)response.StatusCode} - {response.ReasonPhrase} - {body}");
            }
        }

        public static async Task<byte[]> DownloadData(this IAcceptanceTestInfrastructureProvider provider, string url, HttpStatusCode successCode = HttpStatusCode.OK, CancellationToken cancellationToken = default)
        {
            var httpClient = provider.HttpClient;
            var response = await httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode != successCode)
            {
                throw new Exception($"Expected status code of {successCode}, but instead got {response.StatusCode}.");
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        static async Task<T> GetInternal<T>(this IAcceptanceTestInfrastructureProvider provider, string url, CancellationToken cancellationToken) where T : class
        {
            var response = await provider.GetRaw(url, cancellationToken);

            //for now
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent or HttpStatusCode.ServiceUnavailable)
            {
                LogRequest();
                return null;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                LogRequest(response.ReasonPhrase + content);
                throw new InvalidOperationException($"Call failed: {(int)response.StatusCode} - {response.ReasonPhrase} {Environment.NewLine} {content}");
            }

            var payload = await response.Content.ReadFromJsonAsync<T>(provider.SerializerOptions, cancellationToken);
            LogRequest();
            return payload;

            void LogRequest(string additionalInfo = null)
            {
                var additionalInfoString = additionalInfo != null ? ": " + additionalInfo : string.Empty;
                Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}{additionalInfoString}");
            }
        }
    }
}