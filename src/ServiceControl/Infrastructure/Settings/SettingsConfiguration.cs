namespace ServiceBus.Management.Infrastructure.Settings;

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure;
using ServiceControl.Infrastructure.Settings;
using ServiceControl.Infrastructure.WebApi;

public class SettingsValidation(
    ILogger<Settings> logger // Intentionally using SETTINGS as logger name
    ) : IValidateOptions<Settings>// TODO: Register
{
    public ValidateOptionsResult Validate(string name, Settings options)
    {
        List<string> failures = [];

        if (!options.ForwardErrorMessages.HasValue)
        {
            failures.Add("ForwardErrorMessages settings is missing, please make sure it is included.");
        }


        // ErrorRetentionPeriod

        if (options.ErrorRetentionPeriod == TimeSpan.Zero)
        {
            failures.Add("ErrorRetentionPeriod settings is missing, please make sure it is included.");
        }
        else if (options.ErrorRetentionPeriod < TimeSpan.FromDays(5))
        {
            failures.Add("ErrorRetentionPeriod settings is invalid, value should be minimum 5 days");
        }
        else if (options.ErrorRetentionPeriod > TimeSpan.FromDays(45))
        {
            failures.Add("ErrorRetentionPeriod settings is invalid, value should be maximum 45 days");
        }

        // EventRetentionPeriod

        if (options.EventsRetentionPeriod < TimeSpan.FromHours(1))
        {
            failures.Add("EventRetentionPeriod settings is invalid, value should be minimum 1 hour");
        }
        else if (options.EventsRetentionPeriod > TimeSpan.FromDays(200))
        {
            failures.Add("EventRetentionPeriod settings is invalid, value should be maximum 200 days");
        }

        // AuditRetentionPeriod

        if (options.AuditRetentionPeriod < TimeSpan.FromHours(1))
        {
            failures.Add("AuditRetentionPeriod settings is invalid, value should be minimum 1 hour");
        }
        else if (options.AuditRetentionPeriod > TimeSpan.FromDays(365))
        {
            failures.Add("AuditRetentionPeriod settings is invalid, value should be maximum 365 days");
        }

        // TimeToRestartErrorIngestionAfterFailure

        if (options.TimeToRestartErrorIngestionAfterFailure < TimeSpan.FromSeconds(5))
        {
            failures.Add("TimeToRestartErrorIngestionAfterFailure settings is invalid, value should be minimum 5 seconds.");
        }
        else if (options.TimeToRestartErrorIngestionAfterFailure > TimeSpan.FromHours(1))
        {
            failures.Add("TimeToRestartErrorIngestionAfterFailure settings is invalid, value should be maximum 1 hour.");
        }

        // TransportConnectionString

        if (string.IsNullOrEmpty(options.TransportConnectionString))
        {
            failures.Add("TransportConnectionString settings is missing.");
        }

        if (!options.IngestErrorMessages)
        {
            logger.LogInformation("Error ingestion disabled");
        }

        if (string.IsNullOrEmpty(options.ErrorLogQueue))
        {
            logger.LogInformation("No settings found for error log queue to import, default name will be used");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

public class SettingsConfiguration(
    IConfiguration cfg,
    IOptions<LoggingSettings> loggingSettings
) : IConfigureOptions<Settings>// TODO: Register
{
    public void Configure(Settings settings)
    {
        var section = cfg.GetSection(SectionName);

        settings.LoggingSettings = loggingSettings.Value;
        settings.InstanceName = section.GetValue<string>("InstanceName")
                                ?? section.GetValue("InternalQueueName", settings.InstanceName);

        settings.TransportConnectionString = section.GetValue<string>("ConnectionString")
                                             ?? System.Configuration.ConfigurationManager.ConnectionStrings["NServiceBus/Transport"]?.ConnectionString;

        settings.TransportType = section.GetValue<string>("TransportType");
        settings.PersistenceType = section.GetValue<string>("PersistenceType");
        settings.AuditRetentionPeriod = section.GetValue<TimeSpan>("AuditRetentionPeriod");
        settings.ForwardErrorMessages = section.GetValue<bool>("ForwardErrorMessages");
        settings.ErrorRetentionPeriod = section.GetValue<TimeSpan>("ErrorRetentionPeriod");
        settings.EventsRetentionPeriod = section.GetValue("EventRetentionPeriod", settings.EventsRetentionPeriod);
        settings.Port = section.GetValue("Port", settings.Port);
        settings.Hostname = section.GetValue("Hostname", settings.Hostname);
        settings.MaximumConcurrencyLevel = section.GetValue<int?>("MaximumConcurrencyLevel");
        settings.RetryHistoryDepth = section.GetValue("RetryHistoryDepth", settings.RetryHistoryDepth);
        settings.AllowMessageEditing = section.GetValue<bool>("AllowMessageEditing");
        settings.NotificationsFilter = section.GetValue<string>("NotificationsFilter");
        settings.RemoteInstances = GetRemoteInstances(section);
        settings.TimeToRestartErrorIngestionAfterFailure = section.GetValue("TimeToRestartErrorIngestionAfterFailure", settings.TimeToRestartErrorIngestionAfterFailure);
        settings.DisableExternalIntegrationsPublishing = section.GetValue("DisableExternalIntegrationsPublishing", settings.DisableExternalIntegrationsPublishing);
        settings.TrackInstancesInitialValue = section.GetValue("TrackInstancesInitialValue", settings.TrackInstancesInitialValue);
        settings.ShutdownTimeout = section.GetValue("ShutdownTimeout", settings.ShutdownTimeout);
        settings.AssemblyLoadContextResolver = static assemblyPath => new PluginAssemblyLoadContext(assemblyPath);
        settings.ExternalIntegrationsDispatchingBatchSize = section.GetValue("ExternalIntegrationsDispatchingBatchSize", settings.ExternalIntegrationsDispatchingBatchSize);
        settings.InstanceId = InstanceIdGenerator.FromApiUrl(settings.ApiUrl);
        settings.PrintMetrics = section.GetValue<bool>("PrintMetrics");
        settings.VirtualDirectory = section.GetValue("VirtualDirectory", settings.VirtualDirectory);
        settings.HeartbeatGracePeriod = section.GetValue("HeartbeatGracePeriod", settings.HeartbeatGracePeriod);
        settings.IngestErrorMessages = section.GetValue("IngestErrorMessages", settings.IngestErrorMessages);
        settings.ErrorLogQueue = section.GetValue<string>("ErrorLogQueue");
        settings.MaintenanceMode = section.GetValue("MaintenanceMode", settings.MaintenanceMode);
        //settings.ValidateConfiguration = section.GetValue("ValidateConfig", settings.ValidateConfiguration); //TODO: Remove this setting??

        var serviceBusSecton = cfg.GetSection("ServiceBus");
        settings.ErrorQueue = serviceBusSecton.GetValue("ErrorQueue", settings.ErrorQueue); // Previous code was weird, it used a default value, but validation seems it could expect null/empty
        settings.ErrorLogQueue = serviceBusSecton.GetValue<string>("AuditLogQueue", null);

        settings.Name = section.GetValue("Name", settings.Name);
        settings.Description = section.GetValue("Description", settings.Description);
    }

    static RemoteInstanceSetting[] GetRemoteInstances(IConfigurationSection section)
    {
        var valueRead = section.GetValue<string>("RemoteInstances");
        return string.IsNullOrEmpty(valueRead)
            ? []
            : ParseRemoteInstances(valueRead);
    }

    internal static RemoteInstanceSetting[] ParseRemoteInstances(string value) =>
        JsonSerializer.Deserialize<RemoteInstanceSetting[]>(value, SerializerOptions.Default) ?? [];

    public const string SectionName = "ServiceControl";
}

class PostConfiguration(ILogger<Settings> logger) : IPostConfigureOptions<Settings> // TODO: Register
{
    public void PostConfigure(string name, Settings options)
    {
        var suffix = string.Empty;
        if (!string.IsNullOrEmpty(options.VirtualDirectory))
        {
            suffix = $"{options.VirtualDirectory}/";
        }

        options.RootUrl = $"http://{options.Hostname}:{options.Port}/{suffix}";

        if (string.IsNullOrEmpty(options.ErrorLogQueue))
        {
            logger.LogInformation("No settings found for audit log queue to import, default name will be used");
            options.ErrorLogQueue = Subscope(options.ErrorLogQueue);
        }

        if (AppEnvironment.RunningInContainer)
        {
            options.Hostname = "*";
            options.Port = 33333;
        }

    }

    static string Subscope(string address)
    {
        var atIndex = address.IndexOf("@", StringComparison.InvariantCulture);

        if (atIndex <= -1)
        {
            return $"{address}.log";
        }

        var queue = address.Substring(0, atIndex);
        var machine = address.Substring(atIndex + 1);
        return $"{queue}.log@{machine}";
    }
}