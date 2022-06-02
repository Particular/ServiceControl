namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using System.Web.Http;
    using System.Web.Http.Results;
    using Settings;

    class RootController : ApiController
    {
        public RootController(LoggingSettings loggingSettings, Settings settings)
        {
            this.settings = settings;
            this.loggingSettings = loggingSettings;
        }

        [Route("")]
        [HttpGet]
        public OkNegotiatedContentResult<RootUrls> Urls()
        {
            var baseUrl = Url.Content("~/");

            var model = new RootUrls
            {
                KnownEndpointsUrl = "/endpoints/known", // relative URI to allow proxying
                MessageSearchUrl =
                    baseUrl + "messages/search/{keyword}/{?page,per_page,direction,sort}",
                EndpointsMessageSearchUrl =
                    baseUrl +
                    "endpoints/{name}/messages/search/{keyword}/{?page,per_page,direction,sort}",
                EndpointsMessagesUrl =
                    baseUrl + "endpoints/{name}/messages/{?page,per_page,direction,sort}",
                Name = SettingsReader<string>.Read("Name", "ServiceControl.Audit"),
                Description = SettingsReader<string>.Read("Description", "The audit backend for the Particular Service Platform"),
                Configuration = baseUrl + "configuration"
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
                    settings.AuditRetentionPeriod
                },
                PerformanceTunning = new
                {
                    settings.MaxBodySizeToStore,
                    settings.HttpDefaultConnectionLimit,
                    settings.ExpirationProcessBatchSize,
                    settings.ExpirationProcessTimerInSeconds
                },
                Transport = new
                {
                    settings.TransportCustomizationType,
                    settings.AuditLogQueue,
                    settings.AuditQueue,
                    settings.ForwardAuditMessages
                },
                Plugins = new
                {
                }
            };

            return Ok(content);
        }

        readonly LoggingSettings loggingSettings;
        readonly Settings settings;

        public class RootUrls
        {
            public string Description { get; set; }
            public string KnownEndpointsUrl { get; set; }
            public string EndpointsMessageSearchUrl { get; set; }
            public string EndpointsMessagesUrl { get; set; }
            public string Configuration { get; set; }
            public string MessageSearchUrl { get; set; }
            public string Name { get; set; }
        }
    }
}