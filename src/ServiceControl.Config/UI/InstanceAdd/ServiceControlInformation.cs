namespace ServiceControl.Config.UI.InstanceAdd;

using System;
using System.Collections.Generic;
using System.Linq;
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
}