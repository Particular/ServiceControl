namespace ServiceBus.Management.Infrastructure.Nancy.Modules
{
    using System;
    using global::Nancy;
    using Particular.ServiceControl.Licensing;
    using Settings;

    public class RootModule : BaseModule
    {
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
                        Settings.AuditRetentionPeriod,
                        Settings.ErrorRetentionPeriod
                    },
                    PerformanceTunning = new
                    {
                        Settings.MaxBodySizeToStore,
                        Settings.HttpDefaultConnectionLimit,
                        Settings.ExternalIntegrationsDispatchingBatchSize,
                        Settings.ExpirationProcessBatchSize,
                        Settings.ExpirationProcessTimerInSeconds
                    },
                    Transport = new
                    {
                        Settings.TransportCustomizationType,
                        Settings.AuditLogQueue,
                        Settings.AuditQueue,
                        Settings.ErrorLogQueue,
                        Settings.ErrorQueue,
                        Settings.ForwardAuditMessages,
                        Settings.ForwardErrorMessages
                    },
                    Plugins = new
                    {
                        Settings.HeartbeatGracePeriod
                    }
                });

            Get["/instance-info"] = Get["/configuration"] = configuration;
        }

        public LoggingSettings LoggingSettings { get; set; }

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