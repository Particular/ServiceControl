namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Results;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Particular.ServiceControl.Licensing;
    using ServiceBus.Management.Infrastructure.Settings;

    class RootController : ApiController
    {
        public RootController(ActiveLicense license, LoggingSettings loggingSettings, Settings settings, Func<HttpClient> httpClientFactory)
        {
            this.settings = settings;
            this.license = license;
            this.loggingSettings = loggingSettings;
            this.httpClientFactory = httpClientFactory;
        }

        [Route("")]
        [HttpGet]
        public OkNegotiatedContentResult<RootUrls> Urls()
        {
            var baseUrl = Url.Content("~/");

            var model = new RootUrls
            {
                EndpointsUrl = baseUrl + "endpoints",
                KnownEndpointsUrl = "/endpoints/known", // relative URI to allow proxying
                SagasUrl = baseUrl + "sagas",
                ErrorsUrl = baseUrl + "errors/{?page,per_page,direction,sort}",
                EndpointsErrorUrl = baseUrl + "endpoints/{name}/errors/{?page,per_page,direction,sort}",
                MessageSearchUrl =
                    baseUrl + "messages/search/{keyword}/{?page,per_page,direction,sort}",
                EndpointsMessageSearchUrl =
                    baseUrl +
                    "endpoints/{name}/messages/search/{keyword}/{?page,per_page,direction,sort}",
                EndpointsMessagesUrl =
                    baseUrl + "endpoints/{name}/messages/{?page,per_page,direction,sort}",
                AuditCountUrl = baseUrl + "endpoints/{name}/audit-count",
                Name = SettingsReader<string>.Read("Name", "ServiceControl"),
                Description = SettingsReader<string>.Read("Description", "The management backend for the Particular Service Platform"),
                LicenseStatus = license.IsValid ? "valid" : "invalid",
                LicenseDetails = baseUrl + "license",
                Configuration = baseUrl + "configuration",
                RemoteConfiguration = baseUrl + "configuration/remotes",
                EventLogItems = baseUrl + "eventlogitems",
                ArchivedGroupsUrl = baseUrl + "errors/groups/{classifier?}",
                GetArchiveGroup = baseUrl + "archive/groups/id/{groupId}",
            };

            return Ok(model);
        }

        [Route("instance-info")]
        [Route("configuration")]
        [HttpGet]
        public OkNegotiatedContentResult<object> Config()
        {
            object content = new
            {
                Host = new
                {
                    settings.ServiceName,
                    RavenDBPath = settings.DbPath,
                    Logging = new
                    {
                        loggingSettings.LogPath,
                        LoggingLevel = loggingSettings.LoggingLevel.Name,
                        RavenDBLogLevel = loggingSettings.RavenDBLogLevel.Name
                    }
                },
                DataRetention = new
                {
                    settings.AuditRetentionPeriod,
                    settings.ErrorRetentionPeriod
                },
                PerformanceTunning = new
                {
                    settings.HttpDefaultConnectionLimit,
                    settings.ExternalIntegrationsDispatchingBatchSize,
                    //settings.ExpirationProcessBatchSize,  // TODO : Check is this is still needed
                    //settings.ExpirationProcessTimerInSeconds  // TODO : Check is this is still needed
                },
                Transport = new
                {
                    settings.TransportType,
                    settings.ErrorLogQueue,
                    settings.ErrorQueue,
                    settings.ForwardErrorMessages
                },
                Plugins = new
                {
                    settings.HeartbeatGracePeriod
                }
            };

            return Ok(content);
        }

        [Route("configuration/remotes")]
        [HttpGet]
        public async Task<HttpResponseMessage> RemoteConfig()
        {
            var remotes = settings.RemoteInstances;
            var tasks = remotes
                .Select(async remote =>
                {
                    var status = remote.TemporarilyUnavailable ? "unavailable" : "online";
                    var version = "Unknown";
                    var uri = remote.ApiUri.TrimEnd('/') + "/configuration";
                    var httpClient = httpClientFactory();
                    JObject config = null;

                    try
                    {
                        var response = await httpClient.GetAsync(uri).ConfigureAwait(false);

                        if (response.Headers.TryGetValues("X-Particular-Version", out var values))
                        {
                            version = values.FirstOrDefault() ?? "Missing";
                        }

                        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var reader = new StreamReader(stream))
                        using (var jsonReader = new JsonTextReader(reader))
                        {
                            config = jsonSerializer.Deserialize<JObject>(jsonReader);
                        }
                    }
                    catch (Exception)
                    {
                        status = "error";
                    }

                    return new
                    {
                        remote.ApiUri,
                        Version = version,
                        Status = status,
                        Configuration = config
                    };
                })
                .ToArray();

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            return Negotiator.FromModel(Request, results);
        }

        readonly LoggingSettings loggingSettings;
        readonly ActiveLicense license;
        readonly Settings settings;
        readonly Func<HttpClient> httpClientFactory;

        static readonly JsonSerializer jsonSerializer = JsonSerializer.Create(JsonNetSerializerSettings.CreateDefault());

        public class RootUrls
        {
            public string Description { get; set; }
            public string EndpointsErrorUrl { get; set; }
            public string KnownEndpointsUrl { get; set; }
            public string EndpointsMessageSearchUrl { get; set; }
            public string EndpointsMessagesUrl { get; set; }
            public string AuditCountUrl { get; set; }
            public string EndpointsUrl { get; set; }
            public string ErrorsUrl { get; set; }
            public string Configuration { get; set; }
            public string RemoteConfiguration { get; set; }
            public string MessageSearchUrl { get; set; }
            public string LicenseStatus { get; set; }
            public string LicenseDetails { get; set; }
            public string Name { get; set; }
            public string SagasUrl { get; set; }
            public string EventLogItems { get; set; }
            public string ArchivedGroupsUrl { get; set; }
            public string GetArchiveGroup { get; set; }
        }
    }
}