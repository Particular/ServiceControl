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

            Assert.IsTrue(viewModel.InstallErrorInstance);

            Assert.IsTrue(viewModel.InstallAuditInstance);
        }

        [Test]
        public void Transports_are_populated()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.IsNotEmpty(viewModel.Transports);

            Assert.IsNull(viewModel.SelectedTransport);
        }

        [Test]
        public void Transport_connection_string_is_null()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.IsFalse(viewModel.ShowConnectionString);

            Assert.IsNull(viewModel.ConnectionString);

            Assert.IsNull(viewModel.SampleConnectionString);
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

            Assert.AreEqual("LocalSystem", viewModel.ErrorServiceAccount);

            Assert.IsTrue(viewModel.ErrorUseSystemAccount);

            Assert.IsFalse(viewModel.ErrorUseServiceAccount);

            Assert.IsFalse(viewModel.ErrorUseProvidedAccount);

            Assert.IsFalse(viewModel.ErrorPasswordEnabled);

            Assert.IsEmpty(viewModel.ErrorPassword);

            Assert.AreEqual("LocalSystem", viewModel.AuditServiceAccount);

            Assert.IsTrue(viewModel.AuditUseSystemAccount);

            Assert.IsFalse(viewModel.AuditUseServiceAccount);

            Assert.IsFalse(viewModel.AuditUseProvidedAccount);

            Assert.IsFalse(viewModel.AuditPasswordEnabled);

            Assert.IsEmpty(viewModel.AuditPassword);
        }

        [Test]
        public void Hostname_is_local_host()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.AreEqual("localhost", viewModel.ErrorHostName);

            Assert.IsEmpty(viewModel.ErrorHostNameWarning);

            Assert.AreEqual("localhost", viewModel.AuditHostName);

            Assert.IsEmpty(viewModel.AuditHostNameWarning);
        }

        [Test]
        public void Port_number_are_set_to_defaults_with_no_validation_errors()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.AreEqual("33333", viewModel.ErrorPortNumber);

            var errorInfo = (INotifyDataErrorInfo)viewModel;

            var errorPortNumberErrors = errorInfo.GetErrors(nameof(viewModel.ErrorPortNumber));

            Assert.IsEmpty(errorPortNumberErrors);

            Assert.AreEqual("44444", viewModel.AuditPortNumber);

            var auditPortNumberErrors = errorInfo.GetErrors(nameof(viewModel.AuditPortNumber));

            Assert.IsEmpty(auditPortNumberErrors);
        }

        [Test]
        public void Database_maintenance_port_number_are_set_to_defaults_with_no_validation_errors()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.AreEqual("33334", viewModel.ErrorDatabaseMaintenancePortNumber);

            var errorInfo = (INotifyDataErrorInfo)viewModel;

            var errorPortNumberErrors = errorInfo.GetErrors(nameof(viewModel.ErrorDatabaseMaintenancePortNumber));

            Assert.IsEmpty(errorPortNumberErrors);

            Assert.AreEqual("44445", viewModel.AuditDatabaseMaintenancePortNumber);

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

            Assert.AreEqual(TimeSpanUnits.Days, viewModel.ErrorRetentionUnits);

            Assert.AreEqual(SettingConstants.ErrorRetentionPeriodDefaultInDaysForUI, viewModel.ErrorRetention);

            Assert.GreaterOrEqual(viewModel.ErrorRetention, viewModel.MinimumErrorRetentionPeriod);

            Assert.LessOrEqual(viewModel.ErrorRetention, viewModel.MaximumErrorRetentionPeriod);

            Assert.AreEqual(TimeSpanUnits.Days, viewModel.AuditRetentionUnits);

            Assert.AreEqual(SettingConstants.AuditRetentionPeriodDefaultInDaysForUI, viewModel.AuditRetention);

            Assert.GreaterOrEqual(viewModel.AuditRetention, viewModel.MinimumErrorRetentionPeriod);

            Assert.LessOrEqual(viewModel.AuditRetention, viewModel.MaximumErrorRetentionPeriod);
        }

        [Test]
        public void Error_queue_name_has_default_value()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.IsNotEmpty(viewModel.ErrorQueueName);

            Assert.AreEqual("error", viewModel.ErrorQueueName);

            Assert.IsNotEmpty(viewModel.AuditQueueName);

            Assert.AreEqual("audit", viewModel.AuditQueueName);
        }

        [Test]
        public void Error_Forwarding_is_disabled()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.IsNotEmpty(viewModel.ErrorForwardingOptions);

            Assert.AreEqual(false, viewModel.ErrorForwarding.Value);

            Assert.IsNull(viewModel.ErrorForwardingQueueName);

            Assert.IsFalse(viewModel.ShowErrorForwardingQueue);

            Assert.IsNotEmpty(viewModel.AuditForwardingOptions);

            Assert.AreEqual(false, viewModel.AuditForwarding.Value);

            Assert.IsFalse(viewModel.ShowAuditForwardingQueue);
        }

        [Test]
        public void Full_text_search_on_bodies_is_enabled()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.IsNotEmpty(viewModel.ErrorEnableFullTextSearchOnBodiesOptions);

            Assert.AreEqual(true, viewModel.ErrorEnableFullTextSearchOnBodies.Value);

            Assert.IsNotEmpty(viewModel.AuditEnableFullTextSearchOnBodiesOptions);

            Assert.AreEqual(true, viewModel.AuditEnableFullTextSearchOnBodies.Value);
        }
    }
}
