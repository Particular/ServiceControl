namespace ServiceControl.Infrastructure.Api;

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Configuration;
using Monitoring.HeartbeatMonitoring;
using NServiceBus.Hosting;
using Particular.ServiceControl.Licensing;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Api;
using ServiceControl.Api.Contracts;

class ConfigurationApi(ActiveLicense license, Settings settings, IHttpClientFactory httpClientFactory, MassTransitConnectorHeartbeatStatus connectorHeartbeatStatus, HostInformation hostInformation) : IConfigurationApi
{
    public Task<RootUrls> GetUrls(string baseUrl, CancellationToken cancellationToken = default)
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
            MyRoutesUrl = baseUrl + "my/routes",
            PlatformHealth = baseUrl + "platform-health",
        };

        return Task.FromResult(model);
    }


    public Task<object> GetConfig(CancellationToken cancellationToken = default)
    {
        object content = new
        {
            InstanceType = "error",
            HealthChecksEnabled = !settings.DisableHealthChecks,
            Host = new
            {
                settings.InstanceName,
                hostInformation.HostId,
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
                    using var response = await httpClient.GetAsync("api/configuration", cancellationToken);
                    response.EnsureSuccessStatusCode();

                    if (response.Headers.TryGetValues("X-Particular-Version", out var values))
                    {
                        version = values.FirstOrDefault() ?? "Missing";
                    }

                    await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    config = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
                    if (config is not JsonObject configuration ||
                        configuration["host"] is not JsonObject host ||
                        host["instance_name"] is not JsonValue instanceName ||
                        !instanceName.TryGetValue<string>(out var name) || string.IsNullOrWhiteSpace(name))
                    {
                        throw new JsonException("Remote response is not an instance configuration.");
                    }
                }
                catch (HttpRequestException ex)
                {
                    status = ex.StatusCode >= System.Net.HttpStatusCode.InternalServerError ? "error" : "unavailable";
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    status = "unavailable";
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
                    Configuration = status == "online" ? config : null
                };
            });

        var results = await Task.WhenAll(tasks);

        return results;
    }
}