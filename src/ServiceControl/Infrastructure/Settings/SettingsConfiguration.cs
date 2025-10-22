namespace ServiceBus.Management.Infrastructure.Settings;

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceControl.Configuration;
using ServiceControl.Infrastructure.Settings;
using ServiceControl.Infrastructure.WebApi;

public class SettingsValidation(
    ILogger<Settings> logger // Intentionally using SETTINGS as logger name
) : IValidateOptions<PrimaryOptions> // TODO: Register
{
    public ValidateOptionsResult Validate(string name, PrimaryOptions options)
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

        // ConnectionString

        if (string.IsNullOrEmpty(options.ConnectionString))
        {
            failures.Add("ConnectionString settings is missing.");
        }

        if (!options.IngestErrorMessages)
        {
            logger.LogInformation("Error ingestion disabled");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

public class ServiceBusValidation(
    ILogger<Settings> logger // Intentionally using SETTINGS as logger name
) : IValidateOptions<ServiceBusOptions> // TODO: Register
{
    public ValidateOptionsResult Validate(string name, ServiceBusOptions options)
    {
        if (string.IsNullOrEmpty(options.ErrorLogQueue))
        {
            logger.LogInformation("No settings found for error log queue to import, default name will be used");
        }

        return ValidateOptionsResult.Success;
    }
}

public class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public string ErrorLogQueue { get; set; }
    public string ErrorQueue { get; set; } = "error";
}

class ServiceBusOptionsPostConfiguration(ILogger<Settings> logger) : IPostConfigureOptions<ServiceBusOptions> // TODO: Register
{
    public void PostConfigure(string name, ServiceBusOptions options)
    {
        if (string.IsNullOrEmpty(options.ErrorLogQueue))
        {
            logger.LogInformation("No settings found for audit log queue to import, default name will be used");
            options.ErrorLogQueue = Subscope(options.ErrorLogQueue);
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

class PrimaryOptionsPostConfiguration : IPostConfigureOptions<PrimaryOptions> // TODO: Register
{
    public void PostConfigure(string name, PrimaryOptions options)
    {
        var suffix = string.Empty;
        if (!string.IsNullOrEmpty(options.VirtualDirectory))
        {
            suffix = $"{options.VirtualDirectory}/";
        }

        options.RootUrl = $"http://{options.Hostname}:{options.Port}/{suffix}";

        if (AppEnvironment.RunningInContainer)
        {
            options.Hostname = "*";
            options.Port = 33333;
        }

        options.ConnectionString ??= System.Configuration.ConfigurationManager.ConnectionStrings["NServiceBus/Transport"]?.ConnectionString;
        options.InstanceId = InstanceIdGenerator.FromApiUrl(options.ApiUrl);

        options.RemoteInstanceSettings= string.IsNullOrEmpty(options.RemoteInstances)
            ? []
            : ParseRemoteInstances(options.RemoteInstances);
    }

    internal static RemoteInstanceSetting[] ParseRemoteInstances(string value) => JsonSerializer.Deserialize<RemoteInstanceSetting[]>(value, SerializerOptions.Default) ?? [];
}