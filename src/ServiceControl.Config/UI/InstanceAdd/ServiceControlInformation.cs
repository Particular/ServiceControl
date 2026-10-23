namespace ServiceControl.Config.UI.InstanceAdd;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Commands;
using Extensions;
using ServiceControlInstaller.Engine.Configuration.ServiceControl;
using ServiceControlInstaller.Engine.Instances;
using SharedInstanceEditor;
using Xaml.Controls;

public class ServiceControlInformation : SharedServiceControlEditorViewModel
{
    public ServiceControlInformation(ServiceControlEditorViewModel viewModelParent)
    {
        ErrorForwardingOptions = new[]
        {
            new ForwardingOption
            {
                Name = "On",
                Value = true
            },
            new ForwardingOption
            {
                Name = "Off",
                Value = false
            }
        };
        EnableFullTextSearchOnBodiesOptions = new[]
        {
            new EnableFullTextSearchOnBodiesOption
            {
                Name = "On",
                Value = true
            },
            new EnableFullTextSearchOnBodiesOption
            {
                Name = "Off",
                Value = false
            }
        };
        EnableIntegratedServicePulseOptions = new[]
        {
            new EnableIntegratedServicePulseOption
            {
                Name = "On",
                Value = true
            },
            new EnableIntegratedServicePulseOption
            {
                Name = "Off",
                Value = false
            }
        };
        ErrorRetention = SettingConstants.ErrorRetentionPeriodDefaultInDaysForUI;
        Description = "ServiceControl Service";
        HostName = "localhost";
        ErrorQueueName = "error";
        ErrorForwardingQueueName = "error.log";
        ErrorForwarding = ErrorForwardingOptions.First(p => !p.Value); //Default to Off.
        UseSystemAccount = true;
        PortNumber = "33333";
        DatabaseMaintenancePortNumber = "33334";
        EnableFullTextSearchOnBodies = EnableFullTextSearchOnBodiesOptions.First(p => p.Value); //Default to On.
        EnableIntegratedServicePulse = EnableIntegratedServicePulseOptions.First(p => p.Value); //Default to On.
        ViewModelParent = viewModelParent;

        // Only auto-select when there's no real choice to make (picker hidden, RavenDB-only);
        // otherwise leave it unselected so the combo box shows "Please Select...", like transport.
        if (StorageTypes.Count() == 1)
        {
            SelectedStorageType = StorageTypes.First();
        }

        SelectedMessageBodyStorageType = MessageBodyStorageTypes.First();
        SelectedAzureAuthentication = AzureAuthenticationOptions.First();
        SelectMessageBodyPath = new SelectPathCommand(p => MessageBodyPath = p, isFolderPicker: true, defaultPath: MessageBodyPath);
    }

    public int MaximumErrorRetentionPeriod => SettingConstants.ErrorRetentionPeriodMaxInDays;

    public int MinimumErrorRetentionPeriod => SettingConstants.ErrorRetentionPeriodMinInDays;

    public TimeSpanUnits ErrorRetentionUnits => TimeSpanUnits.Days;

    public ServiceControlEditorViewModel ViewModelParent { get; }

    public IEnumerable<PersistenceManifest> StorageTypes { get; } = ServiceControlPersisters.GetSupportedPrimaryManifests();

    // Intentionally not editable after creation - changing storage backend on an existing
    // instance has no supported data migration path yet, so this is only ever set at Add time.
    public PersistenceManifest SelectedStorageType
    {
        get => selectedStorageType;
        set
        {
            StorageConnectionString = null;
            selectedStorageType = value;
        }
    }

    public string StorageConnectionString { get; set; }

    public bool ShowStorageTypePicker => StorageTypes.Count() > 1;

    public bool ShowStorageConnectionString => SelectedStorageType != null && SelectedStorageType.Settings.Any(s => s.Mandatory && s.Name.EndsWith("/ConnectionString"));

    public bool ShowDatabasePathFields => SelectedStorageType == null || SelectedStorageType.Settings.Any(s => s.Name.EndsWith("/DBPath"));

    public bool SupportsMessageBodyStorage => SelectedStorageType?.Settings.Any(s => s.Name == ServiceControlSettings.MessageBodyStorageType.Name) == true;

    public IEnumerable<MessageBodyStorageOption> MessageBodyStorageTypes { get; } =
    [
        new MessageBodyStorageOption { Name = "File system", Value = MessageBodyStorageType.FileSystem },
        new MessageBodyStorageOption { Name = "Azure Blob Storage", Value = MessageBodyStorageType.AzureBlob },
        new MessageBodyStorageOption { Name = "Amazon S3", Value = MessageBodyStorageType.S3 }
    ];

    public MessageBodyStorageOption SelectedMessageBodyStorageType { get; set; }

    public string MessageBodyPath
    {
        get => messageBodyPath;
        set => messageBodyPath = value.SanitizeFilePath();
    }

    public ICommand SelectMessageBodyPath { get; }

    public IEnumerable<AzureAuthenticationOption> AzureAuthenticationOptions { get; } =
    [
        new AzureAuthenticationOption { Name = "Connection string", UsesManagedIdentity = false },
        new AzureAuthenticationOption { Name = "Managed identity", UsesManagedIdentity = true }
    ];

    public AzureAuthenticationOption SelectedAzureAuthentication { get; set; }

    public string AzureConnectionString { get; set; }

    public string AzureServiceUri { get; set; }

    public string AzureManagedIdentityClientId { get; set; }

    public string AzureAuthorityHost { get; set; }

    public string AzureContainerName { get; set; }

    public string S3BucketName { get; set; }

    public string S3KeyPrefix { get; set; }

    public string S3Region { get; set; }

    public string S3ServiceUrl { get; set; }

    public string S3AccessKeyId { get; set; }

    public string S3SecretAccessKey { get; set; }

    // Returns null for storage types that keep message bodies in the database.
    public MessageBodyStorage CreateMessageBodyStorage()
    {
        if (!SupportsMessageBodyStorage)
        {
            return null;
        }

        var storageType = SelectedMessageBodyStorageType.Value;
        var usesManagedIdentity = SelectedAzureAuthentication.UsesManagedIdentity;

        return storageType switch
        {
            MessageBodyStorageType.FileSystem => new MessageBodyStorage { StorageType = storageType, FileSystemStoragePath = MessageBodyPath },
            MessageBodyStorageType.AzureBlob => new MessageBodyStorage
            {
                StorageType = storageType,
                AzureConnectionString = usesManagedIdentity ? null : AzureConnectionString,
                AzureServiceUri = usesManagedIdentity ? AzureServiceUri : null,
                AzureManagedIdentityClientId = usesManagedIdentity ? AzureManagedIdentityClientId : null,
                AzureAuthorityHost = usesManagedIdentity ? AzureAuthorityHost : null,
                AzureContainerName = AzureContainerName
            },
            MessageBodyStorageType.S3 => new MessageBodyStorage
            {
                StorageType = storageType,
                S3BucketName = S3BucketName,
                S3KeyPrefix = S3KeyPrefix,
                S3Region = S3Region,
                S3ServiceUrl = S3ServiceUrl,
                S3AccessKeyId = S3AccessKeyId,
                S3SecretAccessKey = S3SecretAccessKey
            },
            _ => throw new InvalidOperationException($"Unknown message body storage type '{storageType}'.")
        };
    }

    public double ErrorRetention { get; set; }

    public TimeSpan ErrorRetentionPeriod => ErrorRetentionUnits == TimeSpanUnits.Days ? TimeSpan.FromDays(ErrorRetention) : TimeSpan.FromHours(ErrorRetention);

    public string ErrorQueueName { get; set; }

    public string ErrorForwardingQueueName { get; set; }

    public ForwardingOption ErrorForwarding
    {
        get => errorForwarding;
        set
        {
            errorForwarding = value;
            if (value.Value)
            {
                ErrorForwardingQueueName = "error.log";
            }
            else
            {
                ErrorForwardingQueueName = null;
            }
        }
    }

    public string ErrorForwardingWarning => ErrorForwarding != null && ErrorForwarding.Value ? "Only enable if another application is processing messages from the Error Forwarding Queue" : null;

    public IEnumerable<ForwardingOption> ErrorForwardingOptions { get; }

    public IEnumerable<EnableFullTextSearchOnBodiesOption> EnableFullTextSearchOnBodiesOptions { get; }

    public EnableFullTextSearchOnBodiesOption EnableFullTextSearchOnBodies { get; set; }

    public IEnumerable<EnableIntegratedServicePulseOption> EnableIntegratedServicePulseOptions { get; }

    public EnableIntegratedServicePulseOption EnableIntegratedServicePulse { get; set; }

    protected void UpdateErrorRetention(TimeSpan value)
    {
        ErrorRetention = ErrorRetentionUnits == TimeSpanUnits.Days ? value.TotalDays : value.TotalHours;
    }

    // Deliberately not using the OnMethod syntax. The owning viewmodel forwards selected transport changed events manually
    public void SelectedTransportChanged()
    {
        NotifyOfPropertyChange(nameof(ErrorQueueName));
        NotifyOfPropertyChange(nameof(ErrorForwardingQueueName));
    }

    public void ApplyConventionalServiceName(string conventionName)
    {
        InstanceName = GetConventionalServiceName(conventionName);
    }

    public void UpdateFromInstance(ServiceControlInstance instance)
    {
        SetupServiceAccount(instance);
        InstanceName = instance.Name;
        HostName = instance.HostName;
        PortNumber = instance.Port.ToString();
        DatabaseMaintenancePortNumber = instance.DatabaseMaintenancePort.ToString();
        LogPath = instance.LogPath;
        ErrorQueueName = instance.ErrorQueue;
        ErrorForwarding = ErrorForwardingOptions.FirstOrDefault(p => p.Value == instance.ForwardErrorMessages);
        ErrorForwardingQueueName = instance.ErrorLogQueue;
        UpdateErrorRetention(instance.ErrorRetentionPeriod);
        EnableFullTextSearchOnBodies = EnableFullTextSearchOnBodiesOptions.FirstOrDefault(p => p.Value == instance.EnableFullTextSearchOnBodies);
        EnableIntegratedServicePulse = EnableIntegratedServicePulseOptions.FirstOrDefault(p => p.Value == instance.EnableIntegratedServicePulse);
    }

    ForwardingOption errorForwarding;
    PersistenceManifest selectedStorageType;
    string messageBodyPath;
}