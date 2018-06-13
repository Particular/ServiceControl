﻿namespace ServiceBus.Management.Infrastructure.Nancy.Modules
{
    using System;
    using Particular.ServiceControl.Licensing;
    using Settings;
    using global::Nancy;

    public class RootModule : BaseModule
    {
        public LoggingSettings LoggingSettings { get; set; }

        public RootModule()
        {
            Get["/"] = parameters =>
            {
                var model = new RootUrls
                {
                    EndpointsUrl = BaseUrl + "/endpoints",
                    KnownEndpointsUrl = "/endpoints/known", // relative URI to allow proxying
                    SagasUrl = BaseUrl + "/sagas",
                    ErrorsUrl = BaseUrl + "/errors/{?page,per_page,direction,sort}",
                    EndpointsErrorUrl = BaseUrl + "/endpoints/{name}/errors/{?page,per_page,direction,sort}",
                    MessageSearchUrl =
                        BaseUrl + "/messages/search/{keyword}/{?page,per_page,direction,sort}",
                    EndpointsMessageSearchUrl =
                        BaseUrl +
                        "/endpoints/{name}/messages/search/{keyword}/{?page,per_page,direction,sort}",
                    EndpointsMessagesUrl =
                        BaseUrl + "/endpoints/{name}/messages/{?page,per_page,direction,sort}",
                    Name = SettingsReader<string>.Read("Name", "ServiceControl"),
                    Description = SettingsReader<string>.Read("Description", "The management backend for the Particular Service Platform"),
                    LicenseStatus = License.IsValid ? "valid" : "invalid",
                    LicenseDetails = BaseUrl + "/license",
                    Configuration = BaseUrl + "/configuration"
                };

                return Negotiate
                    .WithModel(model);
            };

            Func<dynamic, dynamic> configuration = p => Negotiate
                .WithModel(new
                {
                    Host = new
                    {
                        Settings.ServiceName,
                        RavenDBPath = Settings.DbPath,
                        Logging = new
                        {
                            LoggingSettings.LogPath,
                            LoggingLevel = LoggingSettings.LoggingLevel.Name,
                            RavenDBLogLevel = LoggingSettings.RavenDBLogLevel.Name
                        }
                    },
                    DataRetention = new
                    {
                        AuditRetentionPeriod = Settings.AuditRetentionPeriod,
                        ErrorRetentionPeriod = Settings.ErrorRetentionPeriod
                    },
                    PerformanceTunning = new
                    {
                        MaxBodySizeToStore= Settings.MaxBodySizeToStore,
                        Settings.HttpDefaultConnectionLimit,
                        Settings.ExternalIntegrationsDispatchingBatchSize,
                        Settings.ExpirationProcessBatchSize,
                        Settings.ExpirationProcessTimerInSeconds
                    },
                    Transport = new
                    {
                        Settings.TransportType,
                        AuditLogQueue = Settings.AuditLogQueue,
                        AuditQueue = Settings.AuditQueue,
                        ErrorLogQueue = Settings.ErrorLogQueue,
                        ErrorQueue = Settings.ErrorQueue,
                        Settings.ForwardAuditMessages,
                        Settings.ForwardErrorMessages
                    },
                    Plugins = new
                    {
                        HeartbeatGracePeriod = Settings.HeartbeatGracePeriod
                    }
                });

            Get["/instance-info"] = Get["/configuration"] = configuration;
        }

        public ActiveLicense License { get; set; }

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
        }
    }
}