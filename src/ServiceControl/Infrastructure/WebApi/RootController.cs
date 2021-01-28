namespace ServiceControl.Infrastructure.WebApi
{
    using System.Web.Http;
    using System.Web.Http.Results;
    using Particular.ServiceControl.Licensing;
    using ServiceBus.Management.Infrastructure.Settings;

    public class RootController : ApiController
    {
        internal RootController(ActiveLicense license, LoggingSettings loggingSettings, Settings settings)
        {
            this.settings = settings;
            this.license = license;
            this.loggingSettings = loggingSettings;
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
                Name = SettingsReader<string>.Read("Name", "ServiceControl"),
                Description = SettingsReader<string>.Read("Description", "The management backend for the Particular Service Platform"),
                LicenseStatus = license.IsValid ? "valid" : "invalid",
                LicenseDetails = baseUrl + "license",
                Configuration = baseUrl + "configuration",
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
                    settings.ExpirationProcessBatchSize,
                    settings.ExpirationProcessTimerInSeconds
                },
                Transport = new
                {
                    settings.TransportCustomizationType,
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

        readonly LoggingSettings loggingSettings;

        readonly ActiveLicense license;
        readonly Settings settings;

        public class RootUrls
        {
            public string Description { get; set; }
            public string EndpointsErrorUrl { get; set; }
            public string KnownEndpointsUrl { get; set; }
            public string EndpointsMessageSearchUrl { get; set; }
            public string EndpointsMessagesUrl { get; set; }
            public string EndpointsUrl { get; set; }
            public string ErrorsUrl { get; set; }
            public string Configuration { get; set; }
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