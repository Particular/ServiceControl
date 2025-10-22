namespace ServiceControl.Infrastructure.Api;

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using Monitoring.HeartbeatMonitoring;
using Particular.ServiceControl.Licensing;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Api;
using ServiceControl.Api.Contracts;

class ConfigurationApi(ActiveLicense license,
    Settings settings,
    IHttpClientFactory httpClientFactory, MassTransitConnectorHeartbeatStatus connectorHeartbeatStatus) : IConfigurationApi
{
    public Task<RootUrls> GetUrls(string baseUrl, CancellationToken cancellationToken)
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
            Name = settings.ServiceControl.Name,
            Description = settings.ServiceControl.Description,
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
                settings.ServiceControl.InstanceName,
                Logging = new
                {
                    settings.Logging.LogPath,
                    LoggingLevel = settings.Logging.LogLevel
                }
            },
            DataRetention = new
            {
                settings.ServiceControl.AuditRetentionPeriod,
                settings.ServiceControl.ErrorRetentionPeriod
            },
            PerformanceTunning = new
            {
                settings.ServiceControl.ExternalIntegrationsDispatchingBatchSize
            },
            PersistenceSettings = settings.PersisterSpecificSettings,
            Transport = new
            {
                settings.ServiceControl.TransportType,
                settings.ServiceBus.ErrorLogQueue,
                settings.ServiceBus.ErrorQueue,
                settings.ServiceControl.ForwardErrorMessages
            },
            Plugins = new
            {
                settings.ServiceControl.HeartbeatGracePeriod
            },
            MassTransitConnector = connectorHeartbeatStatus.LastHeartbeat
        };

        return Task.FromResult(content);
    }

    public async Task<RemoteConfiguration[]> GetRemoteConfigs(CancellationToken cancellationToken = default)
    {
        var remotes = settings.ServiceControl.RemoteInstanceSettings;
        var tasks = remotes
            .Select(async remote =>
            {
                string status = "online";
                var version = "Unknown";
                HttpClient httpClient = httpClientFactory.CreateClient(remote.InstanceId);
                JsonNode config = null;

                try
                {
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