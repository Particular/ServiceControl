namespace ServiceBus.Management.Infrastructure.Settings;

using System;
using System.Runtime.Loader;
using System.Text.Json.Serialization;
using NServiceBus.Transport;
using Particular.LicensingComponent.Shared;
using ServiceControl.Infrastructure;
using ServiceControl.Persistence;
using ServiceControl.Transports;

public class Settings
{
    public ServiceControlSettings ServiceControlSettings { get; set; }
    [JsonIgnore] public Func<string, AssemblyLoadContext> AssemblyLoadContextResolver { get; set; }
    public LoggingSettings LoggingSettings { get; set; }
    public string NotificationsFilter { get; set; }
    public bool AllowMessageEditing { get; set; }
    public Func<MessageContext, bool> MessageFilter { get; set; } //HINT: acceptance tests only
    public string EmailDropFolder { get; set; } //HINT: acceptance tests only
    [Obsolete("Likely not needed?", true)] public bool ValidateConfiguration { get; set; } = true;
    public int ExternalIntegrationsDispatchingBatchSize { get; set; } = 100;
    public bool DisableExternalIntegrationsPublishing { get; set; } = false;
    public bool RunCleanupBundle { get; set; }
    public string RootUrl { get; set; }
    public string ApiUrl => $"{RootUrl}api";
    public string InstanceId { get; set; }
    public string StorageUrl => $"{RootUrl}storage";
    public string StagingQueue => $"{InstanceName}.staging";
    public int Port { get; set; } = 33333;
    public PersistenceSettings PersisterSpecificSettings { get; set; }
    public bool PrintMetrics { get; set; }
    public string Hostname { get; set; } = "localhost";
    public string VirtualDirectory { get; set; } = string.Empty;
    public TimeSpan HeartbeatGracePeriod { get; set; } = TimeSpan.FromSeconds(40);
    public string TransportType { get; set; }
    public string PersistenceType { get; set; }
    public string ErrorLogQueue { get; set; }
    public string ErrorQueue { get; set; } = "error";
    public bool? ForwardErrorMessages { get; set; }
    public bool IngestErrorMessages { get; set; } = true;
    public bool RunRetryProcessor { get; set; } = true;
    public TimeSpan? AuditRetentionPeriod { get; set; }
    public TimeSpan ErrorRetentionPeriod { get; set; }
    public TimeSpan EventsRetentionPeriod { get; set; } = TimeSpan.FromDays(14);
    public string InstanceName { get; set; } = DEFAULT_INSTANCE_NAME;
    public bool TrackInstancesInitialValue { get; set; } = true;
    public string TransportConnectionString { get; set; }
    public TimeSpan ProcessRetryBatchesFrequency { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan TimeToRestartErrorIngestionAfterFailure { get; set; } = TimeSpan.FromSeconds(60);
    public int? MaximumConcurrencyLevel { get; set; }
    public int RetryHistoryDepth { get; set; } = 10;
    public RemoteInstanceSetting[] RemoteInstances { get; set; }
    public bool DisableHealthChecks { get; set; }

    // The default value is set to the maximum allowed time by the most
    // restrictive hosting platform, which is Linux containers. Linux
    // containers allow for a maximum of 10 seconds. We set it to 5 to
    // allow for cancellation and logging to take place
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public bool MaintenanceMode { get; set; }
    public string Name { get; set; } = "ServiceControl";
    public string Description { get; set; } = "The management backend for the Particular Service Platform";

    public TransportSettings ToTransportSettings()
    {
        var transportSettings = new TransportSettings
        {
            EndpointName = InstanceName,
            ConnectionString = TransportConnectionString,
            MaxConcurrency = MaximumConcurrencyLevel,
            RunCustomChecks = true,
            TransportType = TransportType,
            AssemblyLoadContextResolver = AssemblyLoadContextResolver
        };
        return transportSettings;
    }

    public const string DEFAULT_INSTANCE_NAME = "Particular.ServiceControl";
}