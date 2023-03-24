﻿namespace ServiceControl.Config.UI.InstanceAdd
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Input;
    using Framework.Rx;
    using PropertyChanged;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using ServiceControlInstaller.Engine.Instances;
    using SharedInstanceEditor;
    using Validar;
    using Validation;
    using Xaml.Controls;

    public class ServiceControlEditorViewModel : RxProgressScreen
    {
        public ServiceControlEditorViewModel()
        {
            Transports = ServiceControlCoreTransports.All.Where(t => t.AvailableInSCMU);
            ServiceControl = new ServiceControlInformation(this);
            ServiceControlAudit = new ServiceControlAuditInformation(this);
        }

        [DoNotNotify]
        public ValidationTemplate ValidationTemplate { get; set; }

        public ICommand Save { get; set; }

        public ICommand Cancel { get; set; }

        public bool SubmitAttempted { get; set; }

        public ServiceControlInformation ServiceControl { get; set; }

        public ServiceControlAuditInformation ServiceControlAudit { get; set; }

        public IEnumerable<TransportInfo> Transports { get; }

        public TransportInfo SelectedTransport
        {
            get => selectedTransport;
            set
            {
                ConnectionString = null;
                selectedTransport = value;
            }
        }

        public bool InstallErrorInstance { get; set; } = true;
        public bool InstallAuditInstance { get; set; } = true;
        public bool OneInstanceTypeSelected => InstallErrorInstance || InstallAuditInstance;

        public string TransportWarning => SelectedTransport?.Help;

        public string ConnectionString { get; set; }

        public string SampleConnectionString => SelectedTransport?.SampleConnectionString;

        public bool ShowConnectionString => !string.IsNullOrEmpty(SelectedTransport?.SampleConnectionString);

        public void OnSubmitAttempted()
        {
            ServiceControl.SubmitAttempted = SubmitAttempted;
            ServiceControlAudit.SubmitAttempted = SubmitAttempted;
        }

        public virtual void OnSelectedTransportChanged()
        {
            ServiceControl?.SelectedTransportChanged();
            ServiceControlAudit?.SelectedTransportChanged();
            NotifyOfPropertyChange(nameof(ConnectionString));
        }

        TransportInfo selectedTransport;
    }

    [InjectValidation]
    public class ServiceControlAddViewModel : ServiceControlEditorViewModel
    {
        public ServiceControlAddViewModel()
        {
            DisplayName = "ADD SERVICECONTROL";
        }

        public string ConventionName { get; set; }

        public void OnConventionNameChanged()
        {
            ServiceControl.ApplyConventionalServiceName(ConventionName);
            ServiceControlAudit.ApplyConventionalServiceName(ConventionName);
        }

        public bool IsServiceControlExpanded { get; set; }
        public bool IsServiceControlAuditExpanded { get; set; }
    }

    [InjectValidation]
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
            ViewModelParent = viewModelParent;
        }

        public int MaximumErrorRetentionPeriod => SettingConstants.ErrorRetentionPeriodMaxInDays;

        public int MinimumErrorRetentionPeriod => SettingConstants.ErrorRetentionPeriodMinInDays;

        public TimeSpanUnits ErrorRetentionUnits => TimeSpanUnits.Days;

        public ServiceControlEditorViewModel ViewModelParent { get; }

        public double ErrorRetention { get; set; }

        public TimeSpan ErrorRetentionPeriod => ErrorRetentionUnits == TimeSpanUnits.Days ? TimeSpan.FromDays(ErrorRetention) : TimeSpan.FromHours(ErrorRetention);

        public string ErrorQueueName { get; set; }

        public string ErrorForwardingQueueName { get; set; }

        public ForwardingOption ErrorForwarding { get; set; }

        [AlsoNotifyFor("ErrorForwarding")]
        public string ErrorForwardingWarning => ErrorForwarding != null && ErrorForwarding.Value ? "Only enable if another application is processing messages from the Error Forwarding Queue" : null;

        public bool ShowErrorForwardingQueue => ErrorForwarding?.Value ?? false;

        public IEnumerable<ForwardingOption> ErrorForwardingOptions { get; }

        public IEnumerable<EnableFullTextSearchOnBodiesOption> EnableFullTextSearchOnBodiesOptions { get; }

        public EnableFullTextSearchOnBodiesOption EnableFullTextSearchOnBodies { get; set; }

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
        }
    }

    [InjectValidation]
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

        public ForwardingOption AuditForwarding { get; set; }

        [AlsoNotifyFor("AuditForwarding")]
        public string AuditForwardingWarning => AuditForwarding != null && AuditForwarding.Value ? "Only enable if another application is processing messages from the Audit Forwarding Queue" : null;

        public bool ShowAuditForwardingQueue => AuditForwarding?.Value ?? false;

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
    }
}