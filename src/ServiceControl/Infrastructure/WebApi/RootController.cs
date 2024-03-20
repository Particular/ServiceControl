namespace ServiceControl.Infrastructure.WebApi
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using Configuration;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Particular.ServiceControl.Licensing;
    using ServiceBus.Management.Infrastructure.Settings;

    [ApiController]
    [Route("api")]
    public class RootController(
        ActiveLicense license,
        Settings settings,
        IHttpClientFactory httpClientFactory)
        : ControllerBase
    {
        [Route("")]
        [HttpGet]
        public RootUrls Urls()
        {
            var baseUrl = Request.GetDisplayUrl() + "/";
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
                Name = SettingsReader.Read(Settings.SettingsRootNamespace, "Name", "ServiceControl"),
                Description = SettingsReader.Read(Settings.SettingsRootNamespace, "Description", "The management backend for the Particular Service Platform"),
                LicenseStatus = license.IsValid ? "valid" : "invalid",
                LicenseDetails = baseUrl + "license",
                Configuration = baseUrl + "configuration",
                RemoteConfiguration = baseUrl + "configuration/remotes",
                EventLogItems = baseUrl + "eventlogitems",
                ArchivedGroupsUrl = baseUrl + "errors/groups/{classifier?}",
                GetArchiveGroup = baseUrl + "archive/groups/id/{groupId}",
            };

            return model;
        }

        [Route("instance-info")]
        [Route("configuration")]
        [HttpGet]
        public object Config()
        {
            object content = new
            {
                Host = new
                {
                    settings.ServiceName,
                    Logging = new
                    {
                        settings.LoggingSettings.LogPath,
                        LoggingLevel = settings.LoggingSettings.LogLevel.Name,
                    }
                },
                DataRetention = new
                {
                    settings.AuditRetentionPeriod,
                    settings.ErrorRetentionPeriod
                },
                PerformanceTunning = new
                {
                    settings.ExternalIntegrationsDispatchingBatchSize
                },
                PersistenceSettings = settings.PersisterSpecificSettings,
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

            return content;
        }

        [Route("configuration/remotes")]
        [HttpGet]
        public async Task<IActionResult> RemoteConfig()
        {
            var remotes = settings.RemoteInstances;
            var tasks = remotes
                .Select(async remote =>
                {
                    var status = remote.TemporarilyUnavailable ? "unavailable" : "online";
                    var version = "Unknown";
                    var httpClient = httpClientFactory.CreateClient(remote.InstanceId);
                    JsonNode config = null;

                    try
                    {
                        var response = await httpClient.GetAsync("/api/configuration");

                        if (response.Headers.TryGetValues("X-Particular-Version", out var values))
                        {
                            version = values.FirstOrDefault() ?? "Missing";
                        }

                        await using var stream = await response.Content.ReadAsStreamAsync();
                        config = await JsonNode.ParseAsync(stream);
                    }
                    catch (Exception)
                    {
                        status = "error";
                    }

                    return new
                    {
                        ApiUri = remote.BaseAddress,
                        Version = version,
                        Status = status,
                        Configuration = config
                    };
                })
                .ToArray();

            var results = await Task.WhenAll(tasks);

            return Ok(results);
        }

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