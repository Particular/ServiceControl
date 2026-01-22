namespace ServiceControl.Infrastructure.Api;

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using Microsoft.Extensions.Logging;
using Monitoring.HeartbeatMonitoring;
using Particular.ServiceControl.Licensing;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Api;
using ServiceControl.Api.Contracts;

class ConfigurationApi(ActiveLicense license, Settings settings, IHttpClientFactory httpClientFactory, MassTransitConnectorHeartbeatStatus connectorHeartbeatStatus, ILogger<ConfigurationApi> logger) : IConfigurationApi
{
    public Task<RootUrls> GetUrls(string baseUrl, CancellationToken cancellationToken)
    {
        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        var model = new RootUrls
        {
            EndpointsUrl = baseUrl + "endpoints",
            KnownEndpointsUrl = "/endpoints/known", // relative URI to allow proxying
            SagasUrl = baseUrl + "sagas",
            ErrorsUrl = baseUrl + "errors/{?page,per_page,direction,sort}",
            EndpointsErrorUrl = baseUrl + "endpoints/{name}/errors/{?page,per_page,direction,sort}",
            MessageSearchUrl = baseUrl + "messages/search/{keyword}/{?page,per_page,direction,sort}",
            EndpointsMessageSearchUrl = baseUrl + "endpoints/{name}/messages/search/{keyword}/{?page,per_page,direction,sort}",
            EndpointsMessagesUrl = baseUrl + "endpoints/{name}/messages/{?page,per_page,direction,sort}",
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

        return Task.FromResult(model);
    }


    public Task<object> GetConfig(CancellationToken cancellationToken)
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
            },
            MassTransitConnector = connectorHeartbeatStatus.LastHeartbeat
        };

        return Task.FromResult(content);
    }

    public async Task<RemoteConfiguration[]> GetRemoteConfigs(CancellationToken cancellationToken = default)
    {
        var remotes = settings.RemoteInstances;
        var tasks = remotes
            .Select(async remote =>
            {
                string status = "online";
                var version = "Unknown";
                HttpClient httpClient = httpClientFactory.CreateClient(remote.InstanceId);
                JsonNode config = null;

                try
                {
                    // Remote config queries don't forward authentication
                    logger.LogDebug("Remote config: GET {BaseAddress}/api/configuration (no auth header)", remote.BaseAddress);

                    using var response = await httpClient.GetAsync("/api/configuration", cancellationToken);

                    if (response.Headers.TryGetValues("X-Particular-Version", out var values))
                    {
                        version = values.FirstOrDefault() ?? "Missing";
                    }

                    await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    config = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
                }
                catch (HttpRequestException ex)
                {
                    status = ex.StatusCode >= System.Net.HttpStatusCode.InternalServerError ? "error" : "unavailable";
                }
                catch (Exception)
                {
                    status = "error";
                }

                return new RemoteConfiguration
                {
                    ApiUri = remote.BaseAddress,
                    Version = version,
                    Status = status,
                    Configuration = config
                };
            });

        var results = await Task.WhenAll(tasks);

        return results;
    }
}