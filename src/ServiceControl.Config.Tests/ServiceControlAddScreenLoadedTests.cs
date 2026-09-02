namespace ServiceControl.Config.Tests
{
    using System;
    using System.ComponentModel;
    using System.Linq;
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

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.InstallErrorInstance, Is.True);
                Assert.That(viewModel.InstallAuditInstance, Is.True);
            }
        }

        [Test]
        public void Transports_are_populated()
        {
            var viewModel = new ServiceControlAddViewModel();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.Transports, Is.Not.Empty);
                Assert.That(viewModel.SelectedTransport, Is.Null);
            }
        }

        [Test]
        public void Transport_connection_string_is_null()
        {
            var viewModel = new ServiceControlAddViewModel();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ShowConnectionString, Is.False);
                Assert.That(viewModel.ConnectionString, Is.Null);
                Assert.That(viewModel.SampleConnectionString, Is.Null);
            }
        }

        [Test]
        public void ErrorForwardingOptions_are_populated()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.That(viewModel.ErrorForwardingOptions, Is.Not.Empty);
        }

        [Test]
        public void FullTextSearchOptions_are_populated()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.That(viewModel.ErrorEnableFullTextSearchOnBodiesOptions, Is.Not.Empty);
        }

        [Test]
        public void User_account_is_set_to_local_system()
        {
            var viewModel = new ServiceControlAddViewModel();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ErrorServiceAccount, Is.EqualTo("LocalSystem"));
                Assert.That(viewModel.ErrorUseSystemAccount, Is.True);
                Assert.That(viewModel.ErrorUseServiceAccount, Is.False);
                Assert.That(viewModel.ErrorUseProvidedAccount, Is.False);
                Assert.That(viewModel.ErrorPasswordEnabled, Is.False);
                Assert.That(viewModel.ErrorPassword, Is.Empty);
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.AuditServiceAccount, Is.EqualTo("LocalSystem"));
                Assert.That(viewModel.AuditUseSystemAccount, Is.True);
                Assert.That(viewModel.AuditUseServiceAccount, Is.False);
                Assert.That(viewModel.AuditUseProvidedAccount, Is.False);
                Assert.That(viewModel.AuditPasswordEnabled, Is.False);
                Assert.That(viewModel.AuditPassword, Is.Empty);
            }
        }

        [Test]
        public void Hostname_is_local_host()
        {
            var viewModel = new ServiceControlAddViewModel();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ErrorHostName, Is.EqualTo("localhost"));
                Assert.That(viewModel.ErrorHostNameWarning, Is.Empty);
                Assert.That(viewModel.AuditHostName, Is.EqualTo("localhost"));
                Assert.That(viewModel.AuditHostNameWarning, Is.Empty);
            }
        }

        [Test]
        public void Port_number_are_set_to_defaults_with_no_validation_errors()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.That(viewModel.ErrorPortNumber, Is.EqualTo("33333"));

            var errorInfo = (INotifyDataErrorInfo)viewModel;

            var errorPortNumberErrors = errorInfo.GetErrors(nameof(viewModel.ErrorPortNumber));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(errorPortNumberErrors, Is.Empty);
                Assert.That(viewModel.AuditPortNumber, Is.EqualTo("44444"));
            }

            var auditPortNumberErrors = errorInfo.GetErrors(nameof(viewModel.AuditPortNumber));

            Assert.That(auditPortNumberErrors, Is.Empty);
        }

        [Test]
        public void Database_maintenance_port_number_are_set_to_defaults_with_no_validation_errors()
        {
            var viewModel = new ServiceControlAddViewModel();

            Assert.That(viewModel.ErrorDatabaseMaintenancePortNumber, Is.EqualTo("33334"));

            var errorInfo = (INotifyDataErrorInfo)viewModel;

            var errorPortNumberErrors = errorInfo.GetErrors(nameof(viewModel.ErrorDatabaseMaintenancePortNumber));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(errorPortNumberErrors, Is.Empty);
                Assert.That(viewModel.AuditDatabaseMaintenancePortNumber, Is.EqualTo("44445"));
            }

            var auditPortNumberErrors = errorInfo.GetErrors(nameof(viewModel.AuditDatabaseMaintenancePortNumber));

            Assert.That(auditPortNumberErrors, Is.Empty);
        }


        [Test]
        public void Destination_path_is_null()
        {
            var viewModel = new ServiceControlAddViewModel(() => []);

            var errorInfo = (INotifyDataErrorInfo)viewModel;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ErrorDestinationPath, Is.EqualTo($@"{programX86Path}\Particular Software\Particular.ServiceControl"));
                Assert.That(errorInfo.GetErrors(nameof(viewModel.ErrorDestinationPath)), Is.Empty);
                Assert.That(viewModel.AuditDestinationPath, Is.EqualTo($@"{programX86Path}\Particular Software\Particular.ServiceControl.Audit"));
                Assert.That(errorInfo.GetErrors(nameof(viewModel.AuditDestinationPath)), Is.Empty);
            }
        }

        [Test]
        public void Log_path_is_null()
        {
            var viewModel = new ServiceControlAddViewModel(() => []);

            var errorInfo = (INotifyDataErrorInfo)viewModel;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ErrorLogPath, Is.EqualTo($@"{programDataPath}\Particular\ServiceControl\Particular.ServiceControl\Logs"));
                Assert.That(errorInfo.GetErrors(nameof(viewModel.ErrorLogPath)), Is.Empty);
                Assert.That(viewModel.AuditLogPath, Is.EqualTo($@"{programDataPath}\Particular\ServiceControl\Particular.ServiceControl.Audit\Logs"));
                Assert.That(errorInfo.GetErrors(nameof(viewModel.AuditLogPath)), Is.Empty);
            }
        }


        [Test]
        public void Database_path_is_null()
        {
            var viewModel = new ServiceControlAddViewModel(() => []);

            var errorInfo = (INotifyDataErrorInfo)viewModel;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ErrorDatabasePath, Is.EqualTo($@"{programDataPath}\Particular\ServiceControl\Particular.ServiceControl\DB"));
                Assert.That(errorInfo.GetErrors(nameof(viewModel.ErrorDatabasePath)), Is.Empty);
                Assert.That(viewModel.AuditDatabasePath, Is.EqualTo($@"{programDataPath}\Particular\ServiceControl\Particular.ServiceControl.Audit\DB"));
                Assert.That(errorInfo.GetErrors(nameof(viewModel.AuditDatabasePath)), Is.Empty);
            }
        }

        [Test]
        public void Retention_Period_is_set_to_default_days()
        {
            var viewModel = new ServiceControlAddViewModel();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ErrorRetentionUnits, Is.EqualTo(TimeSpanUnits.Days));
                Assert.That(viewModel.ErrorRetention, Is.EqualTo(SettingConstants.ErrorRetentionPeriodDefaultInDaysForUI));
                Assert.That(viewModel.ErrorRetention, Is.GreaterThanOrEqualTo(viewModel.MinimumErrorRetentionPeriod));
                Assert.That(viewModel.ErrorRetention, Is.LessThanOrEqualTo(viewModel.MaximumErrorRetentionPeriod));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.AuditRetentionUnits, Is.EqualTo(TimeSpanUnits.Days));
                Assert.That(viewModel.AuditRetention, Is.EqualTo(SettingConstants.AuditRetentionPeriodDefaultInDaysForUI));
                Assert.That(viewModel.AuditRetention, Is.GreaterThanOrEqualTo(viewModel.MinimumErrorRetentionPeriod));
                Assert.That(viewModel.AuditRetention, Is.LessThanOrEqualTo(viewModel.MaximumErrorRetentionPeriod));
            }
        }

        [Test]
        public void Error_queue_name_has_default_value()
        {
            var viewModel = new ServiceControlAddViewModel();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ErrorQueueName, Is.Not.Empty);
                Assert.That(viewModel.ErrorQueueName, Is.EqualTo("error"));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.AuditQueueName, Is.Not.Empty);
                Assert.That(viewModel.AuditQueueName, Is.EqualTo("audit"));
            }
        }

        [Test]
        public void Error_Forwarding_is_disabled()
        {
            var viewModel = new ServiceControlAddViewModel();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ErrorForwardingOptions, Is.Not.Empty);
                Assert.That(viewModel.ErrorForwarding.Value, Is.EqualTo(false));
                Assert.That(viewModel.ErrorForwardingQueueName, Is.Null);
                Assert.That(viewModel.ShowErrorForwardingQueue, Is.False);
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.AuditForwardingOptions, Is.Not.Empty);
                Assert.That(viewModel.AuditForwarding.Value, Is.EqualTo(false));
                Assert.That(viewModel.ShowAuditForwardingQueue, Is.False);
            }
        }

        [Test]
        public void Full_text_search_on_bodies_is_enabled()
        {
            var viewModel = new ServiceControlAddViewModel();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ErrorEnableFullTextSearchOnBodiesOptions, Is.Not.Empty);
                Assert.That(viewModel.ErrorEnableFullTextSearchOnBodies.Value, Is.EqualTo(true));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.AuditEnableFullTextSearchOnBodiesOptions, Is.Not.Empty);
                Assert.That(viewModel.AuditEnableFullTextSearchOnBodies.Value, Is.EqualTo(true));
            }
        }

        [Test]
        public void Instance_sections_are_expanded_by_default()
        {
            var viewModel = new ServiceControlAddViewModel();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.IsServiceControlExpanded, Is.True);
                Assert.That(viewModel.IsServiceControlAuditExpanded, Is.True);
            }
        }

        [Test]
        public void Integrated_ServicePulse_is_enabled_by_default()
        {
            var viewModel = new ServiceControlAddViewModel();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ErrorEnableIntegratedServicePulseOptions, Is.Not.Empty);
                Assert.That(viewModel.ErrorEnableIntegratedServicePulse.Value, Is.True);
            }
        }

        [Test]
        public void Integrated_ServicePulse_can_be_disabled()
        {
            var viewModel = new ServiceControlAddViewModel();

            var offOption = viewModel.ErrorEnableIntegratedServicePulseOptions.First(o => !o.Value);
            viewModel.ServiceControl.EnableIntegratedServicePulse = offOption;

            Assert.That(viewModel.ErrorEnableIntegratedServicePulse.Value, Is.False);
        }

        [Test]
        public void Audit_only_configuration_has_no_validation_errors_for_error_fields()
        {
            var viewModel = new ServiceControlAddViewModel(() => [])
            {
                InstallErrorInstance = false,
                InstallAuditInstance = true,
                SubmitAttempted = true
            };

            var notifyErrorInfo = (INotifyDataErrorInfo)viewModel;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorInstanceName)), Is.Empty);
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorHostName)), Is.Empty);
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorPortNumber)), Is.Empty);
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDestinationPath)), Is.Empty);
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorLogPath)), Is.Empty);
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDatabasePath)), Is.Empty);
            }
        }

        [Test]
        public void Error_only_configuration_has_no_validation_errors_for_audit_fields()
        {
            var viewModel = new ServiceControlAddViewModel(() => [])
            {
                InstallErrorInstance = true,
                InstallAuditInstance = false,
                SubmitAttempted = true
            };

            var notifyErrorInfo = (INotifyDataErrorInfo)viewModel;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditInstanceName)), Is.Empty);
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditHostName)), Is.Empty);
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditPortNumber)), Is.Empty);
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDestinationPath)), Is.Empty);
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditLogPath)), Is.Empty);
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDatabasePath)), Is.Empty);
            }
        }
    }
}
