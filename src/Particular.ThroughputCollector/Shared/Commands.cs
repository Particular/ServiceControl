namespace Particular.ThroughputCollector.Shared
{
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;
    using Particular.ThroughputCollector.Contracts;
    using Particular.ThroughputCollector.Infrastructure;

    class Commands
    {
        static readonly Version MinAuditCountsVersion = new Version(4, 29);

        public static async Task<ServiceControlEndpoint[]> GetKnownEndpoints(ServiceControlClient primary, ILogger logger, CancellationToken cancellationToken)
        {
            // Tool can't proceed without this data, try 5 times
            var arr = await primary.GetData<JArray>("/endpoints", 5, cancellationToken).ConfigureAwait(false);

            var endpoints = arr.Select(endpointToken => new
            {
                Name = endpointToken["name"]?.Value<string>(),
                HeartbeatsEnabled = endpointToken["monitored"]?.Value<bool>(),
                ReceivingHeartbeats = endpointToken["heartbeat_information"]?["reported_status"]!.Value<string>() == "beating"
            })
            .GroupBy(x => x.Name)
            .Select(g => new ServiceControlEndpoint
            {
                Name = g.Key!,
                HeartbeatsEnabled = g.Any(e => e.HeartbeatsEnabled == true),
                ReceivingHeartbeats = g.Any(e => e.ReceivingHeartbeats)
            })
            .ToArray();

            // Verify audit instances also have audit counts
            var remotesInfoJson = await primary.GetData<JArray>("/configuration/remotes", cancellationToken).ConfigureAwait(false);
            var remoteInfo = remotesInfoJson.Select(remote =>
            {
                var uri = remote["api_uri"]?.Value<string>();
                var status = remote["status"]?.Value<string>();
                var versionString = remote["version"]?.Value<string>();
                var retentionString = remote["configuration"]?["data_retention"]?["audit_retention_period"]?.Value<string>();

                return new
                {
                    Uri = uri,
                    Status = status,
                    VersionString = versionString,
                    SemVer = SemVerVersion.TryParse(versionString, out var v) ? v : null,
                    Retention = TimeSpan.TryParse(retentionString, out var ts) ? ts : TimeSpan.Zero
                };
            })
            .ToArray();

            foreach (var remote in remoteInfo)
            {
                if (remote.Status == "online" || remote.SemVer is not null)
                {
                    logger.LogInformation($"ServiceControl Audit instance at {remote.Uri} detected running version {remote.SemVer}");
                }
                else
                {
                    var configUrl = primary.GetFullUrl("/configuration/remotes");
                    var remoteConfigMsg = $"Unable to determine the version of one or more ServiceControl Audit instances. For the instance with URI {remote.Uri}, the status was '{remote.Status}' and the version string returned was '{remote.VersionString}'. If you are not able to resolve this issue on your own, send the contents of {configUrl} to Particular when requesting help.";
                    throw new HaltException(HaltReason.InvalidEnvironment, remoteConfigMsg);
                }
            }

            // Want 2d audit retention so we get one complete UTC day no matter what time it is.
            // Customers are expected to run at least version 4.29 for their Audit instances
            var allHaveAuditCounts = remoteInfo.All(r => r.SemVer?.Version >= MinAuditCountsVersion && r.Retention >= TimeSpan.FromDays(2));
            if (!allHaveAuditCounts)
            {
                logger.LogWarning($"At least one ServiceControl Audit instance is either not running the required version ({MinAuditCountsVersion}) or is not configured for at least 2 days of retention. Audit throughput will not be available.");
            }

            foreach (var endpoint in endpoints)
            {
                var path = $"/endpoints/{endpoint.UrlName}/audit-count";
                try
                {
                    endpoint.AuditCounts = await primary.GetData<AuditCount[]>(path, 2, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, $"Audit count not available on endpoint {endpoint.Name} at {path}");
                }

                //if (useAuditCounts)
                //{
                //    var path = $"/endpoints/{endpoint.UrlName}/audit-count";
                //    endpoint.AuditCounts = await primary.GetData<AuditCount[]>(path, 2, cancellationToken).ConfigureAwait(false);
                //}
                //else
                //{
                //    var path = $"/endpoints/{endpoint.UrlName}/messages/?per_page=1";
                //    var recentMessages = await primary.GetData<JArray>(path, 2, cancellationToken).ConfigureAwait(false);
                //    endpoint.NoAuditCounts = recentMessages.Any();
                //}
            }

            return endpoints;
        }

        public static async Task<Dictionary<string, string>> GetBrokerSettingValues(BrokerSettings brokerSettings, string transportConnectionString, string serviceControlAPI, ILogger logger)
        {
            var brokerSettingValues = new Dictionary<string, string>();

            brokerSettings.Settings.ForEach(s => brokerSettingValues.Add(s.Name, ""));

            //for each broker try and grab the required settings from config/env, and if they don't exist try to get them from the transportConnectionString
            switch (brokerSettings.Broker)
            {
                case Broker.ServiceControl:
                    logger.LogInformation("Not using a broker - throughput data will come from ServiceControl.");
                    brokerSettingValues[ServiceControlSettings.API] = GetConfigSetting(ServiceControlSettings.API, logger);
                    if (string.IsNullOrEmpty(brokerSettingValues[ServiceControlSettings.API]))
                    {
                        brokerSettingValues[ServiceControlSettings.API] = serviceControlAPI;
                    }
                    break;
                case Broker.AmazonSQS:
                    brokerSettingValues[AmazonSQSSettings.Profile] = GetConfigSetting(AmazonSQSSettings.Profile, logger);
                    brokerSettingValues[AmazonSQSSettings.Region] = GetConfigSetting(AmazonSQSSettings.Region, logger);
                    brokerSettingValues[AmazonSQSSettings.Prefix] = GetConfigSetting(AmazonSQSSettings.Prefix, logger);
                    //TODO if those settings don't exist - try and get them from transportConnectionString 
                    break;
                case Broker.RabbitMQ:
                    brokerSettingValues[RabbitMQSettings.API] = GetConfigSetting(RabbitMQSettings.API, logger);
                    brokerSettingValues[RabbitMQSettings.UserName] = GetConfigSetting(RabbitMQSettings.UserName, logger);
                    brokerSettingValues[RabbitMQSettings.Password] = GetConfigSetting(RabbitMQSettings.Password, logger);
                    //TODO if those settings don't exist - try and get them from transportConnectionString 
                    break;
                case Broker.AzureServiceBus:
                    brokerSettingValues[AzureServiceBusSettings.ResourceId] = GetConfigSetting(AzureServiceBusSettings.ResourceId, logger);
                    brokerSettingValues[AzureServiceBusSettings.ClientId] = GetConfigSetting(AzureServiceBusSettings.ClientId, logger);
                    brokerSettingValues[AzureServiceBusSettings.ClientSecret] = GetConfigSetting(AzureServiceBusSettings.ClientSecret, logger);
                    //TODO if those settings don't exist - try and get them from transportConnectionString 
                    break;
                case Broker.SqlServer:
                    brokerSettingValues[SqlServerSettings.ConnectionString] = GetConfigSetting(SqlServerSettings.ConnectionString, logger);
                    if (string.IsNullOrEmpty(brokerSettingValues[SqlServerSettings.ConnectionString]))
                    {
                        brokerSettingValues[SqlServerSettings.ConnectionString] = transportConnectionString;
                    }
                    brokerSettingValues[SqlServerSettings.AdditionalCatalogs] = GetConfigSetting(SqlServerSettings.AdditionalCatalogs, logger);
                    break;
                default:
                    break;
            }

            return await Task.FromResult(brokerSettingValues).ConfigureAwait(false);
        }

        static string GetConfigSetting(string name, ILogger logger)
        {
            logger.LogInformation($"Finding setting for {name}");

            //TODO - how are we handling getting settings?
            return string.Empty;
        }
    }
}
