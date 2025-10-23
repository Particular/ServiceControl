namespace ServiceBus.Management.Infrastructure.Settings;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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