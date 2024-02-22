namespace Particular.License.Throughput.Audit
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Particular.License.Infrastructure;

    class ServiceControlClient
    {
        static readonly JsonSerializer serializer = new JsonSerializer();

        readonly Func<HttpClient> httpFactory;
        readonly string rootUrl;
        readonly string paramName;
        readonly string instanceType;
        readonly ILogger logger;

        public SemVerVersion? Version { get; private set; }

        public ServiceControlClient(string paramName, string instanceType, string rootUrl, Func<HttpClient> httpFactory, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(rootUrl))
            {
                throw new HaltException(HaltReason.InvalidConfig, $"The {paramName} option specifying the {instanceType} URL was not provided.");
            }

            this.paramName = paramName;
            this.instanceType = instanceType;
            this.rootUrl = rootUrl.TrimEnd('/');
            this.httpFactory = httpFactory;
            this.logger = logger;
        }

        public Task<TJsonType> GetData<TJsonType>(string pathAndQuery, CancellationToken cancellationToken = default)
        {
            return GetData<TJsonType>(pathAndQuery, 1, cancellationToken);
        }

        public string GetFullUrl(string pathAndQuery)
        {
            if (pathAndQuery is null || !pathAndQuery.StartsWith('/'))
            {
                throw new ArgumentException("pathAndQuery must start with a forward slash.");
            }

            return rootUrl + pathAndQuery;
        }

        public async Task<TJsonType> GetData<TJsonType>(string pathAndQuery, int tryCount, CancellationToken cancellationToken = default)
        {
            var url = GetFullUrl(pathAndQuery);

            using var http = httpFactory();

            for (int i = 0; i < tryCount; i++)
            {
                try
                {
                    using (var stream = await http.GetStreamAsync(url, cancellationToken).ConfigureAwait(false))
                    using (var reader = new StreamReader(stream))
                    using (var jsonReader = new JsonTextReader(reader))
                    {
#pragma warning disable CS8603 // Possible null reference return.
                        return serializer.Deserialize<TJsonType>(jsonReader);
#pragma warning restore CS8603 // Possible null reference return.
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception x)
                {
                    if (i + 1 >= tryCount)
                    {
                        throw new ServiceControlDataException(url, tryCount, x);
                    }
                }
            }

            throw new InvalidOperationException("Retry loop ended without returning or throwing. This should not happen.");
        }

        public async Task CheckEndpoint(Func<string, bool> contentTest, CancellationToken cancellationToken = default)
        {
            using var http = httpFactory();

            HttpResponseMessage? res = null;
            try
            {
                res = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, rootUrl), cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException hx)
            {
                throw new HaltException(HaltReason.InvalidEnvironment, $"The server at {rootUrl} did not respond. The exception message was: {hx.Message}");
            }

            if (!res.IsSuccessStatusCode)
            {
                var resultErrorMsg = new StringBuilder($"The server at {rootUrl} returned a non-successful status code: {(int)res.StatusCode} {res.StatusCode}")
                    .AppendLine()
                    .AppendLine("Response Headers:");

                foreach (var header in res.Headers)
                {
                    _ = resultErrorMsg.AppendLine($"  {header.Key}: {header.Value}");
                }

                throw new HaltException(HaltReason.RuntimeError, resultErrorMsg.ToString());
            }

            if (!res.Headers.TryGetValues("X-Particular-Version", out var versionHeaders))
            {
                throw new HaltException(HaltReason.InvalidConfig, $"The server at {rootUrl} specified by parameter {paramName} does not appear to be a ServiceControl instance. Are you sure you have the right URL?");
            }

            Version = versionHeaders.Select(header => SemVerVersion.ParseOrDefault(header)).FirstOrDefault();
            logger.LogInformation($"{instanceType} instance at {rootUrl} detected running version {Version}");

            var content = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!contentTest(content))
            {
                throw new HaltException(HaltReason.InvalidConfig, $"The server at {rootUrl} specified by parameter {paramName} does not appear to be a {instanceType} instance. Are you sure you have the right URL?");
            }
        }
    }
}
