namespace ServiceControl.Infrastructure.Api
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;
    using Configuration;
    using Particular.ServiceControl.Licensing;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Api;
    using ServiceControl.Api.Contracts;

    class ConfigurationApi(ActiveLicense license,
        LoggingSettings loggingSettings,
        Settings settings,
        IHttpClientFactory httpClientFactory) : IConfigurationApi
    {
        public RootUrls GetUrls(string baseUrl)
        {
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


        public object GetConfig()
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
                    settings.AuditRetentionPeriod,
                    settings.ErrorRetentionPeriod
                },
                PerformanceTunning = new
                {
                    settings.HttpDefaultConnectionLimit,
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

        public async Task<RemoteConfiguration[]> GetRemoteConfigs()
        {
            var remotes = settings.RemoteInstances;
            var tasks = remotes
                .Select(async remote =>
                {
                    string status =
                        remote.TemporarilyUnavailable ? RemoteStatus.Unavailable : RemoteStatus.Online;
                    var version = "Unknown";
                    using HttpClient httpClient = httpClientFactory.CreateClient(remote.InstanceId);
                    InstanceConfiguration config = null;

                    try
                    {
                        using HttpResponseMessage response = await httpClient.GetAsync("/api/configuration");

                        if (response.Headers.TryGetValues("X-Particular-Version", out var values))
                        {
                            version = values.FirstOrDefault() ?? "Missing";
                        }

                        config = await response.Content.ReadFromJsonAsync<InstanceConfiguration>();
                    }
                    catch (Exception)
                    {
                        status = RemoteStatus.Error;
                    }

                    return new RemoteConfiguration
                    {
                        ApiUri = remote.BaseAddress,
                        Version = version,
                        Status = status,
                        Configuration = config
                    };
                })
                .ToArray();

            var results = await Task.WhenAll(tasks);

            return results;
        }
    }
}
