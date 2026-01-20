namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using Configuration;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Settings;

    [ApiController]
    [Route("api")]
    public class RootController : ControllerBase
    {
        public RootController(Settings settings)
        {
            this.settings = settings;
        }

        // This endpoint is used for health checks by the primary instance. As its a service-to-service call, it needs to be anonymous.
        [AllowAnonymous]
        [Route("")]
        [HttpGet]
        public OkObjectResult Urls()
        {
            var baseUrl = Request.GetDisplayUrl();

            if (!baseUrl.EndsWith('/'))
            {
                baseUrl += "/";
            }

            var model = new RootUrls
            {
                KnownEndpointsUrl = "/endpoints/known", // relative URI to allow proxying
                MessageSearchUrl = baseUrl + "messages/search/{keyword}/{?page,per_page,direction,sort}",
                EndpointsMessageSearchUrl = baseUrl + "endpoints/{name}/messages/search/{keyword}/{?page,per_page,direction,sort}",
                EndpointsMessagesUrl = baseUrl + "endpoints/{name}/messages/{?page,per_page,direction,sort}",
                AuditCountUrl = baseUrl + "endpoints/{name}/audit-count",
                Name = SettingsReader.Read(Settings.SettingsRootNamespace, "Name", "ServiceControl.Audit"),
                Description = SettingsReader.Read(Settings.SettingsRootNamespace, "Description", "The audit backend for the Particular Service Platform"),
                Configuration = baseUrl + "configuration"
            };

            return Ok(model);
        }

        // This endpoint is used by the primary instance to get the config of remotes. As its a service-to-service call, it needs to be anonymous.
        [AllowAnonymous]
        [Route("instance-info")]
        [Route("configuration")]
        [HttpGet]
        public OkObjectResult Config()
        {
            object content = new
            {
                Host = new
                {
                    settings.InstanceName,
                    Logging = new
                    {
                        settings.LoggingSettings.LogPath,
                        LoggingLevel = settings.LoggingSettings.LogLevel
                    }
                },
                DataRetention = new
                {
                    settings.AuditRetentionPeriod
                },
                PerformanceTunning = new
                {
                    settings.MaxBodySizeToStore,
                },
                Transport = new
                {
                    settings.TransportType,
                    settings.AuditLogQueue,
                    settings.AuditQueue,
                    settings.ForwardAuditMessages
                },
                Peristence = new
                {
                    settings.PersistenceType
                },
                Plugins = new
                {
                }
            };

            return Ok(content);
        }

        readonly Settings settings;

        public class RootUrls
        {
            public string Description { get; set; }
            public string KnownEndpointsUrl { get; set; }
            public string EndpointsMessageSearchUrl { get; set; }
            public string EndpointsMessagesUrl { get; set; }
            public string AuditCountUrl { get; set; }
            public string Configuration { get; set; }
            public string MessageSearchUrl { get; set; }
            public string Name { get; set; }
        }
    }
}