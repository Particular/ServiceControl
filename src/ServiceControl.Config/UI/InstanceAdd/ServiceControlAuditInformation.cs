namespace ServiceControl.Config.UI.InstanceAdd;

using System;
using System.Collections.Generic;
using System.Linq;
using ServiceControlInstaller.Engine.Configuration.ServiceControl;
using ServiceControlInstaller.Engine.Instances;
using SharedInstanceEditor;
using Xaml.Controls;

public class ServiceControlAuditInformation : SharedServiceControlEditorViewModel
{
    public ServiceControlAuditInformation(ServiceControlEditorViewModel viewModelParent)
    {
        AuditForwardingOptions = new[]
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
        AuditRetention = SettingConstants.AuditRetentionPeriodDefaultInDaysForUI;
        Description = "ServiceControl Audit";
        HostName = "localhost";
        AuditQueueName = "audit";
        AuditForwardingQueueName = "audit.log";
        AuditForwarding = AuditForwardingOptions.First(p => !p.Value); //Default to Off.
        UseSystemAccount = true;
        PortNumber = "44444";
        DatabaseMaintenancePortNumber = "44445";
        EnableFullTextSearchOnBodies = EnableFullTextSearchOnBodiesOptions.First(p => p.Value); //Default to On.
        ViewModelParent = viewModelParent;
    }

    public ServiceControlEditorViewModel ViewModelParent { get; }

    public int MinimumAuditRetentionPeriod => SettingConstants.AuditRetentionPeriodMinInDays;

    public int MaximumAuditRetentionPeriod => SettingConstants.AuditRetentionPeriodMaxInDays;

    public TimeSpanUnits AuditRetentionUnits => TimeSpanUnits.Days;

    public double AuditRetention { get; set; }

    public TimeSpan AuditRetentionPeriod => AuditRetentionUnits == TimeSpanUnits.Days ? TimeSpan.FromDays(AuditRetention) : TimeSpan.FromHours(AuditRetention);

    public IEnumerable<ForwardingOption> AuditForwardingOptions { get; }

    public string AuditQueueName { get; set; }

    public string AuditForwardingQueueName { get; set; }

    public ForwardingOption AuditForwarding
    {
        get => auditForwarding;
        set
        {
            auditForwarding = value;
            if (value.Value)
            {
                AuditForwardingQueueName = "audit.log";
            }
            else
            {
                AuditForwardingQueueName = null;
            }
        }
    }

    public string AuditForwardingWarning => AuditForwarding != null && AuditForwarding.Value ? "Only enable if another application is processing messages from the Audit Forwarding Queue" : null;


    public IEnumerable<EnableFullTextSearchOnBodiesOption> EnableFullTextSearchOnBodiesOptions { get; }

    public EnableFullTextSearchOnBodiesOption EnableFullTextSearchOnBodies { get; set; }

    public void SetFullTextSearchOnBodies(bool? enabled) =>
        EnableFullTextSearchOnBodies = EnableFullTextSearchOnBodiesOptions
            .FirstOrDefault(p => p.Value == enabled);

    protected void UpdateAuditRetention(TimeSpan value)
    {
        AuditRetention = AuditRetentionUnits == TimeSpanUnits.Days ? value.TotalDays : value.TotalHours;
    }

    // Deliberately not using the OnMethod syntax. The owning viewmodel forwards selected transport changed events manually
    public void SelectedTransportChanged()
    {
        NotifyOfPropertyChange(nameof(AuditQueueName));
        NotifyOfPropertyChange(nameof(AuditForwardingQueueName));
    }

    public void ApplyConventionalServiceName(string conventionName)
    {
        var serviceName = GetConventionalServiceName(conventionName);

        if (!serviceName.EndsWith(".Audit", StringComparison.InvariantCultureIgnoreCase))
        {
            serviceName += ".Audit";
        }

        InstanceName = serviceName;
    }

    public void UpdateFromInstance(ServiceControlAuditInstance instance)
    {
        SetupServiceAccount(instance);
        InstanceName = instance.Name;
        Description = instance.Description;
        HostName = instance.HostName;
        PortNumber = instance.Port.ToString();
        DatabaseMaintenancePortNumber = instance.DatabaseMaintenancePort.ToString();
        LogPath = instance.LogPath;
        AuditForwardingQueueName = instance.AuditLogQueue;
        AuditQueueName = instance.AuditQueue;
        AuditForwarding = AuditForwardingOptions.FirstOrDefault(p => p.Value == instance.ForwardAuditMessages);
        UpdateAuditRetention(instance.AuditRetentionPeriod);
        EnableFullTextSearchOnBodies = EnableFullTextSearchOnBodiesOptions.FirstOrDefault(p => p.Value == instance.EnableFullTextSearchOnBodies);
    }
    ForwardingOption auditForwarding;
}