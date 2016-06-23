﻿namespace ServiceControl.Config.UI.SharedInstanceEditor
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Input;
    using PropertyChanged;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControl.Config.Validation;
    using ServiceControl.Config.Xaml.Controls;
    using ServiceControlInstaller.Engine.Configuration;
    using ServiceControlInstaller.Engine.Instances;

    public class ForwardingOption
    {
        public string Name { get; set; }
        public bool Value { get; set; }
    }

    public class SharedInstanceEditorViewModel : RxProgressScreen
    {
        string hostName;
        string serviceAccount;
        string password;
        TransportInfo selectedTransport;

        public SharedInstanceEditorViewModel()
        {
            Transports = ServiceControlInstaller.Engine.Instances.Transports.All;
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
        }

        [DoNotNotify]
        public ValidationTemplate ValidationTemplate { get; set; }

        public string InstanceName { get; set; }

        public string HostName
        {
            get { return hostName; }
            set
            {
                if (!string.Equals("localhost", value, StringComparison.InvariantCulture))
                {
                    HostNameWarning = Validations.WRN_HOSTNAME_SHOULD_BE_LOCALHOST;
                }
                else
                {
                    HostNameWarning = string.Empty;
                }

                hostName = value;
            }
        }

        public string HostNameWarning { get; set; }

        public string SelectedTransportWarning { get; set; }

        public string PortNumber { get; set; }

        public string Description { get; set; }

        public string ServiceAccount
        {
            get
            {
                if (UseProvidedAccount)
                {
                    return serviceAccount;
                }

                if (UseSystemAccount)
                {
                    return "LocalSystem";
                }

                return "LocalService";
            }
            set { serviceAccount = value; }
        }

        public string Password
        {
            get { return UseProvidedAccount ? password : String.Empty; }
            set { password = value; }
        }

        public bool UseSystemAccount { get; set; }

        public bool UseServiceAccount { get; set; }

        public bool UseProvidedAccount { get; set; }

        public string ErrorQueueName { get; set; }
        public string ErrorForwardingQueueName { get; set; }
        public string AuditQueueName { get; set; }
        public string AuditForwardingQueueName { get; set; }

        public ForwardingOption AuditForwarding { get; set; }
        public ForwardingOption ErrorForwarding { get; set; }

        public int MaximumErrorRetentionPeriod => SettingConstants.ErrorRetentionPeriodMaxInDays;
        public int MinimumErrorRetentionPeriod => SettingConstants.ErrorRetentionPeriodMinInDays;
        public TimeSpanUnits ErrorRetentionUnits => TimeSpanUnits.Days;

        public int MinimumAuditRetentionPeriod => SettingConstants.AuditRetentionPeriodMinInHours;
        public int MaximumAuditRetentionPeriod => SettingConstants.AuditRetentionPeriodMaxInHours;
        public TimeSpanUnits AuditRetentionUnits => TimeSpanUnits.Hours;

        public IEnumerable<ForwardingOption> AuditForwardingOptions{ get; private set;}
        public IEnumerable<ForwardingOption> ErrorForwardingOptions { get; private set; }

        public double AuditRetention { get; set; }
        public TimeSpan AuditRetentionPeriod => AuditRetentionUnits == TimeSpanUnits.Days ? TimeSpan.FromDays(AuditRetention) : TimeSpan.FromHours(AuditRetention);

        public double ErrorRetention { get; set; }
        public TimeSpan ErrorRetentionPeriod => ErrorRetentionUnits == TimeSpanUnits.Days ? TimeSpan.FromDays(ErrorRetention) : TimeSpan.FromHours(ErrorRetention);

        public IEnumerable<TransportInfo> Transports { get; private set; }

        protected void UpdateAuditRetention(TimeSpan value)
        {
            AuditRetention = AuditRetentionUnits == TimeSpanUnits.Days ? value.TotalDays : value.TotalHours;
        }

        protected void UpdateErrorRetention(TimeSpan value)
        {
            ErrorRetention = ErrorRetentionUnits == TimeSpanUnits.Days ? value.TotalDays : value.TotalHours;
        }

        [AlsoNotifyFor("ConnectionString", "ErrorQueueName", "AuditQueueName", "ErrorForwardingQueueName", "AuditForwardingQueueName")]
        public TransportInfo SelectedTransport
        {
            get { return selectedTransport; }
            set
            {
                ConnectionString = null;
                selectedTransport = value;

                SelectedTransportWarning = selectedTransport.Warning;
            }
        }

        public string ConnectionString { get; set; }
        
        // ReSharper disable once UnusedMember.Global
        public string SampleConnectionString => SelectedTransport != null ? SelectedTransport.SampleConnectionString : String.Empty;

        // ReSharper disable once UnusedMember.Global
        public bool ShowConnectionString => SelectedTransport != null && !string.IsNullOrEmpty(SelectedTransport.SampleConnectionString);

        public string LogPath { get; set; }
        public ICommand SelectLogPath { get; set; }

        public ICommand Save { get; set; }
        public ICommand Cancel { get; set; }
        
        public bool SubmitAttempted { get; set; }

        protected virtual void OnInstanceNameChanged()
        {
        }
    }
}