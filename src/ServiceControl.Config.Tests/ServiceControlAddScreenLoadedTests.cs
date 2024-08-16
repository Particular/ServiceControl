namespace ServiceControl.Config.Tests
{
    using System;
    using System.ComponentModel;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using UI.InstanceAdd;
    using Xaml.Controls;

    class AddErrorInstanceScreenLoadedTests
    {
        static readonly string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        static readonly string programX86Path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        [Test]
        public void Error_and_Audit_Instances_are_selected_for_install()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.That(viewModel.InstallErrorInstance, Is.True);

            Assert.That(viewModel.InstallAuditInstance, Is.True);
        }

        [Test]
        public void Transports_are_populated()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.IsNotEmpty(viewModel.Transports);

            Assert.That(viewModel.SelectedTransport, Is.Null);
        }

        [Test]
        public void Transport_connection_string_is_null()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.That(viewModel.ShowConnectionString, Is.False);

            Assert.That(viewModel.ConnectionString, Is.Null);

            Assert.That(viewModel.SampleConnectionString, Is.Null);
        }

        [Test]
        public void ErrorForwardingOptions_are_populated()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.IsNotEmpty(viewModel.ErrorForwardingOptions);
        }

        [Test]
        public void FullTextSearchOptions_are_populated()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.IsNotEmpty(viewModel.ErrorEnableFullTextSearchOnBodiesOptions);
        }

        [Test]
        public void User_account_is_set_to_local_system()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.That(viewModel.ErrorServiceAccount, Is.EqualTo("LocalSystem"));

            Assert.That(viewModel.ErrorUseSystemAccount, Is.True);

            Assert.That(viewModel.ErrorUseServiceAccount, Is.False);

            Assert.That(viewModel.ErrorUseProvidedAccount, Is.False);

            Assert.That(viewModel.ErrorPasswordEnabled, Is.False);

            Assert.IsEmpty(viewModel.ErrorPassword);

            Assert.That(viewModel.AuditServiceAccount, Is.EqualTo("LocalSystem"));

            Assert.That(viewModel.AuditUseSystemAccount, Is.True);

            Assert.That(viewModel.AuditUseServiceAccount, Is.False);

            Assert.That(viewModel.AuditUseProvidedAccount, Is.False);

            Assert.That(viewModel.AuditPasswordEnabled, Is.False);

            Assert.IsEmpty(viewModel.AuditPassword);
        }

        [Test]
        public void Hostname_is_local_host()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.That(viewModel.ErrorHostName, Is.EqualTo("localhost"));

            Assert.IsEmpty(viewModel.ErrorHostNameWarning);

            Assert.That(viewModel.AuditHostName, Is.EqualTo("localhost"));

            Assert.IsEmpty(viewModel.AuditHostNameWarning);
        }

        [Test]
        public void Port_number_are_set_to_defaults_with_no_validation_errors()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.That(viewModel.ErrorPortNumber, Is.EqualTo("33333"));

            var errorInfo = (INotifyDataErrorInfo)viewModel;

            var errorPortNumberErrors = errorInfo.GetErrors(nameof(viewModel.ErrorPortNumber));

            Assert.IsEmpty(errorPortNumberErrors);

            Assert.That(viewModel.AuditPortNumber, Is.EqualTo("44444"));

            var auditPortNumberErrors = errorInfo.GetErrors(nameof(viewModel.AuditPortNumber));

            Assert.IsEmpty(auditPortNumberErrors);
        }

        [Test]
        public void Database_maintenance_port_number_are_set_to_defaults_with_no_validation_errors()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.That(viewModel.ErrorDatabaseMaintenancePortNumber, Is.EqualTo("33334"));

            var errorInfo = (INotifyDataErrorInfo)viewModel;

            var errorPortNumberErrors = errorInfo.GetErrors(nameof(viewModel.ErrorDatabaseMaintenancePortNumber));

            Assert.IsEmpty(errorPortNumberErrors);

            Assert.That(viewModel.AuditDatabaseMaintenancePortNumber, Is.EqualTo("44445"));

            var auditPortNumberErrors = errorInfo.GetErrors(nameof(viewModel.AuditDatabaseMaintenancePortNumber));

            Assert.IsEmpty(auditPortNumberErrors);
        }


        [Test]
        public void Destination_path_is_null()
        {
            var viewModel = new ServiceControlAddViewModel();

            var errorInfo = (INotifyDataErrorInfo)viewModel;

            Assert.That(viewModel.ErrorDestinationPath, Is.EqualTo($@"{programX86Path}\Particular Software\Particular.ServiceControl"));

            Assert.IsEmpty(errorInfo.GetErrors(nameof(viewModel.ErrorDestinationPath)));

            Assert.That(viewModel.AuditDestinationPath, Is.EqualTo($@"{programX86Path}\Particular Software\Particular.ServiceControl.Audit"));

            Assert.IsEmpty(errorInfo.GetErrors(nameof(viewModel.AuditDestinationPath)));
        }

        [Test]
        public void Log_path_is_null()
        {
            var viewModel = new ServiceControlAddViewModel();

            var errorInfo = (INotifyDataErrorInfo)viewModel;

            Assert.That(viewModel.ErrorLogPath, Is.EqualTo($@"{programDataPath}\Particular\ServiceControl\Particular.ServiceControl\Logs"));

            Assert.IsEmpty(errorInfo.GetErrors(nameof(viewModel.ErrorLogPath)));

            Assert.That(viewModel.AuditLogPath, Is.EqualTo($@"{programDataPath}\Particular\ServiceControl\Particular.ServiceControl.Audit\Logs"));

            Assert.IsEmpty(errorInfo.GetErrors(nameof(viewModel.AuditLogPath)));
        }


        [Test]
        public void Database_path_is_null()
        {
            var viewModel = new ServiceControlAddViewModel();

            var errorInfo = (INotifyDataErrorInfo)viewModel;

            Assert.That(viewModel.ErrorDatabasePath, Is.EqualTo($@"{programDataPath}\Particular\ServiceControl\Particular.ServiceControl\DB"));

            Assert.IsEmpty(errorInfo.GetErrors(nameof(viewModel.ErrorDatabasePath)));

            Assert.That(viewModel.AuditDatabasePath, Is.EqualTo($@"{programDataPath}\Particular\ServiceControl\Particular.ServiceControl.Audit\DB"));

            Assert.IsEmpty(errorInfo.GetErrors(nameof(viewModel.AuditDatabasePath)));
        }

        [Test]
        public void Retention_Period_is_set_to_default_days()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.That(viewModel.ErrorRetentionUnits, Is.EqualTo(TimeSpanUnits.Days));

            Assert.That(viewModel.ErrorRetention, Is.EqualTo(SettingConstants.ErrorRetentionPeriodDefaultInDaysForUI));

            Assert.That(viewModel.ErrorRetention, Is.GreaterThanOrEqualTo(viewModel.MinimumErrorRetentionPeriod));

            Assert.That(viewModel.ErrorRetention, Is.LessThanOrEqualTo(viewModel.MaximumErrorRetentionPeriod));

            Assert.That(viewModel.AuditRetentionUnits, Is.EqualTo(TimeSpanUnits.Days));

            Assert.That(viewModel.AuditRetention, Is.EqualTo(SettingConstants.AuditRetentionPeriodDefaultInDaysForUI));

            Assert.That(viewModel.AuditRetention, Is.GreaterThanOrEqualTo(viewModel.MinimumErrorRetentionPeriod));

            Assert.That(viewModel.AuditRetention, Is.LessThanOrEqualTo(viewModel.MaximumErrorRetentionPeriod));
        }

        [Test]
        public void Error_queue_name_has_default_value()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.IsNotEmpty(viewModel.ErrorQueueName);

            Assert.That(viewModel.ErrorQueueName, Is.EqualTo("error"));

            Assert.IsNotEmpty(viewModel.AuditQueueName);

            Assert.That(viewModel.AuditQueueName, Is.EqualTo("audit"));
        }

        [Test]
        public void Error_Forwarding_is_disabled()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.IsNotEmpty(viewModel.ErrorForwardingOptions);

            Assert.That(viewModel.ErrorForwarding.Value, Is.EqualTo(false));

            Assert.That(viewModel.ErrorForwardingQueueName, Is.Null);

            Assert.That(viewModel.ShowErrorForwardingQueue, Is.False);

            Assert.IsNotEmpty(viewModel.AuditForwardingOptions);

            Assert.That(viewModel.AuditForwarding.Value, Is.EqualTo(false));

            Assert.That(viewModel.ShowAuditForwardingQueue, Is.False);
        }

        [Test]
        public void Full_text_search_on_bodies_is_enabled()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.IsNotEmpty(viewModel.ErrorEnableFullTextSearchOnBodiesOptions);

            Assert.That(viewModel.ErrorEnableFullTextSearchOnBodies.Value, Is.EqualTo(true));

            Assert.IsNotEmpty(viewModel.AuditEnableFullTextSearchOnBodiesOptions);

            Assert.That(viewModel.AuditEnableFullTextSearchOnBodies.Value, Is.EqualTo(true));
        }
    }
}
