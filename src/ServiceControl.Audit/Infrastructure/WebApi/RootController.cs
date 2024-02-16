﻿namespace ServiceControl.Audit.Infrastructure.WebApi
{
    using System;
    using Configuration;
    using Microsoft.AspNetCore.Mvc;
    using Settings;

    [ApiController]
    [Route("api")]
    public class RootController : ControllerBase
    {
        public RootController(LoggingSettings loggingSettings, Settings settings)
        {
            this.settings = settings;
            this.loggingSettings = loggingSettings;
        }

        [Route("")]
        [HttpGet]
        public OkObjectResult Urls()
        {
            var baseUrl = new UriBuilder(Request.Scheme, Request.Host.Host, Request.Host.Port ?? -1).Uri.AbsoluteUri;

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
                AuditCountUrl = baseUrl + "endpoints/{name}/audit-count",
                Name = SettingsReader.Read(Settings.SettingsRootNamespace, "Name", "ServiceControl.Audit"),
                Description = SettingsReader.Read(Settings.SettingsRootNamespace, "Description", "The audit backend for the Particular Service Platform"),
                Configuration = baseUrl + "configuration"
            };

            return Ok(model);
        }

        [Route("instance-info")]
        [Route("configuration")]
        [HttpGet]
        public OkObjectResult Config()
        {
            object content = new
            {
                Host = new
                {
                    settings.ServiceName,
                    Logging = new
                    {
                        loggingSettings.LogPath,
                        LoggingLevel = loggingSettings.LoggingLevel.Name,
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

        readonly LoggingSettings loggingSettings;
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