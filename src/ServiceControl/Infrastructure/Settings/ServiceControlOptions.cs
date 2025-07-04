namespace ServiceControl.Infrastructure.Settings;

using System;
using System.IO;
using ServiceControl.Configuration;

public class ServiceControlOptions
{
    const string DefaultInstanceName = "Particular.ServiceControl";
    const int DefaultExternalIntegrationsDispatchingBatchSize = 100;
    static readonly string DefaultLogPath = Path.Combine(AppContext.BaseDirectory, ".logs");

    [AppConfigSetting(
        "ServiceControl/InternalQueueName", // LEGACY SETTING NAME
        "ServiceControl/InstanceName")]
    public string InstanceName { get; set; } = DefaultInstanceName;

    [AppConfigSetting("ServiceControl/LogLevel")]
    public string LogLevel { get; set; }

    // SC installer always populates LogPath in app.config on installation/change/upgrade so this will only be used when
    // debugging or if the entry is removed manually. In those circumstances default to the folder containing the exe
    [AppConfigSetting("ServiceControl/LogPath")]
    public string LogPath { get; set; } = DefaultLogPath;

    [AppConfigSetting("ServiceControl/TransportType")]
    public string TransportType { get; set; }

    [AppConfigSetting("ServiceBus/ErrorQueue")]
    public string ErrorQueue { get; set; }

    [AppConfigSetting("ServiceBus/ErrorLogQueue")]
    public string ErrorLogQueue { get; set; }

    public bool ForwardErrorMessages { get; set; }

    [AppConfigSetting("ServiceControl/ErrorRetentionPeriod")]
    public TimeSpan ErrorRetentionPeriod { get; set; }

    [AppConfigSetting("ServiceControl/AuditRetentionPeriod")]
    public TimeSpan? AuditRetentionPeriod { get; set; }

    [AppConfigSetting("ServiceControl/HeartbeatGracePeriod")]
    public TimeSpan HeartbeatGracePeriod { get; set; } = TimeSpan.FromSeconds(40);

    public string StagingQueue => $"{InstanceName}.staging";

    [AppConfigSetting("ServiceControl/DisableExternalIntegrationsPublishing")]
    public bool DisableExternalIntegrationsPublishing { get; set; } = false;

    [AppConfigSetting("ServiceControl/ExternalIntegrationsDispatchingBatchSize")]
    public int ExternalIntegrationsDispatchingBatchSize { get; set; } = DefaultExternalIntegrationsDispatchingBatchSize;
}
