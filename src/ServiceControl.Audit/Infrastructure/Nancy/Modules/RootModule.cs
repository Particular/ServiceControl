namespace ServiceControl.Audit.Infrastructure.Nancy.Modules
{
    using System;
    using global::Nancy;
    using Settings;

    class RootModule : BaseModule
    {
        public RootModule()
        {
            Get["/"] = parameters =>
            {
                var model = new RootUrls
                {
                    KnownEndpointsUrl = "/endpoints/known", // relative URI to allow proxying
                    MessageSearchUrl =
                        BaseUrl + "/messages/search/{keyword}/{?page,per_page,direction,sort}",
                    EndpointsMessageSearchUrl =
                        BaseUrl +
                        "/endpoints/{name}/messages/search/{keyword}/{?page,per_page,direction,sort}",
                    EndpointsMessagesUrl =
                        BaseUrl + "/endpoints/{name}/messages/{?page,per_page,direction,sort}",
                    Name = SettingsReader<string>.Read("Name", "ServiceControl.Audit"),
                    Description = SettingsReader<string>.Read("Description", "The audit backend for the Particular Service Platform"),
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
                    },
                    PerformanceTunning = new
                    {
                        Settings.MaxBodySizeToStore,
                        Settings.HttpDefaultConnectionLimit,
                        Settings.ExpirationProcessBatchSize,
                        Settings.ExpirationProcessTimerInSeconds
                    },
                    Transport = new
                    {
                        Settings.TransportCustomizationType,
                        Settings.AuditLogQueue,
                        Settings.AuditQueue,
                        Settings.ForwardAuditMessages,
                    },
                    Plugins = new
                    {
                    }
                });

            Get["/instance-info"] = Get["/configuration"] = configuration;
        }

        public LoggingSettings LoggingSettings { get; set; }

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