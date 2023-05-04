﻿namespace ServiceControl.Config.Tests.Validation
{
    using System.ComponentModel;
    using System.Linq;
    using System.ServiceProcess;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using UI.InstanceAdd;

    public class AddErrorInstanceValidationTests
    {

        #region ValidateConventionName

        [Test]
        public void Convention_name_cannot_be_empty_when_instance_names_are_not_provided()
        {
            var viewModel = new ServiceControlAddViewModel();

            var instanceNamesProvided =
                  (viewModel.InstallErrorInstance
                   && !string.IsNullOrWhiteSpace(viewModel.ErrorInstanceName))
               || (viewModel.InstallAuditInstance
                   && !string.IsNullOrWhiteSpace(viewModel.AuditInstanceName));

            viewModel.SubmitAttempted = true;

            //Triggers validation without setting convention name since that would affect the instance names
            viewModel.NotifyOfPropertyChange("ConventionName");

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsFalse(instanceNamesProvided);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ConventionName)));
        }

        [Test]
        public void Convention_name_can_be_empty_when_instance_names_are_provided()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                ServiceControl =
                {
                    InstanceName = "Foo"
                }
            };

            var instanceNamesProvided =
                    (viewModel.InstallErrorInstance
                     && !string.IsNullOrWhiteSpace(viewModel.ErrorInstanceName))
                 || (viewModel.InstallAuditInstance
                     && !string.IsNullOrWhiteSpace(viewModel.AuditInstanceName));

            viewModel.SubmitAttempted = true;

            viewModel.ConventionName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsTrue(instanceNamesProvided, "Instance names were not provided.");

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ConventionName)));
        }

        [Test]
        public void When_convention_name_not_empty_instance_names_should_include_convention_name_overwriting_previous_names()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                ErrorInstanceName = "Error",
                AuditInstanceName = "Audit",
                ConventionName = "Something"
            };

            Assert.AreEqual($"Particular.{viewModel.ConventionName}", viewModel.ErrorInstanceName);

            Assert.AreEqual($"Particular.{viewModel.ConventionName}.Audit", viewModel.AuditInstanceName);
        }

        #endregion

        #region transportname

        [Test]
        public void Transport_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                SelectedTransport = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.SelectedTransport));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.SelectedTransport));

            Assert.IsNotEmpty(errors);
        }

        [TestCase(TransportNames.AmazonSQS)]
        [TestCase(TransportNames.AzureServiceBus)]
        [TestCase(TransportNames.SQLServer)]
        [TestCase(TransportNames.RabbitMQClassicDirectRoutingTopology)]
        public void Transport_connection_string_cannot_be_empty_if_sample_connection_string_is_present_when_adding_error_instance(
            string transportInfoName)
        {

            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName),
                SubmitAttempted = true,
                ConnectionString = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsNotEmpty(errors);
        }

        [TestCase(TransportNames.AmazonSQS)]
        [TestCase(TransportNames.AzureServiceBus)]
        [TestCase(TransportNames.SQLServer)]
        [TestCase(TransportNames.RabbitMQClassicDirectRoutingTopology)]
        public void Transport_connection_string_cannot_be_null_if_sample_connection_string_is_present_when_adding_error_instance(
            string transportInfoName)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName),
                SubmitAttempted = true,
                ConnectionString = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ConnectionString));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsNotEmpty(errors);

        }
        #endregion

        #region errorinstancename

        [Test]
        public void Error_instance_name_cannot_be_same_as_an_existing_windows_service_when_adding_error_instance()
        {
            var windowsServices = ServiceController.GetServices();

            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorInstanceName = windowsServices.First().ServiceName
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorInstanceName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Error_instance_name_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorInstanceName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorInstanceName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Error_instance_name_can_be_empty_when_not_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                SubmitAttempted = true,
                InstallErrorInstance = false,
                ErrorInstanceName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorInstanceName));

            Assert.IsEmpty(errors);
        }

        #endregion

        #region useraccountinfo

        [Test]
        public void User_account_info_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                SubmitAttempted = true,
                InstallErrorInstance = true
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var selectedAccount = viewModel.ErrorServiceAccount;

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorServiceAccount));

            Assert.AreEqual("LocalSystem", selectedAccount);

            Assert.IsEmpty(errors);
        }

        [Test]
        public void Account_name_cannot_be_empty_if_custom_user_account_is_selected_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                SubmitAttempted = true,
                InstallErrorInstance = true,
                ErrorUseProvidedAccount = true,
                ErrorServiceAccount = string.Empty,
                ErrorPassword = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errorServiceAccount = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorServiceAccount));

            Assert.IsNotEmpty(errorServiceAccount);

        }

        #endregion

        #region hostname

        [Test]
        public void Error_hostname_can_not_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorHostName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorHostName)));
        }

        [Test]
        public void Error_hostname_can_not_be_null_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorHostName = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorHostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorHostName)));
        }

        [Test]
        public void Error_hostname_can_be_empty_when_not_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = false,
                SubmitAttempted = true,
                AuditHostName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorHostName)));
        }

        [Test]
        public void Error_hostname_can_be_null_when_not_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = false,
                SubmitAttempted = true,
                ErrorHostName = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorHostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorHostName)));
        }

        [TestCase("192.168.1.1")]
        [TestCase("256.0.0.0")]
        public void Error_hostname_can_be_an_ip_address_when_adding_error_instance(string ipAddress)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorHostName = ipAddress
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorHostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorHostName)));
        }

        #endregion

        #region Portnumber
        [Test]
        public void Port_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorPortNumber = null
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorPortNumber));

            Assert.IsNotEmpty(errors);
        }
        [Test]
        public void Port_is_not_in_valid_range_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorPortNumber = "50000"
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorPortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        [Explicit]
        public void Port_can_not_be_a_port_in_use_by_the_operating_system_when_adding_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorPortNumber = "33333"
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorPortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorPortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Error_port_is_not_equal_to_database_port_number_when_adding_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorDatabaseMaintenancePortNumber = "33333",
                ErrorPortNumber = "33333"
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorPortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorPortNumber));

            Assert.IsNotEmpty(errors);
        }

        #endregion

        #region DatabaseManintenancePortnumber
        [Test]
        public void Database_maintenance_port_cannot_be_empty_when_adding_error_instance()
        {

            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorDatabaseMaintenancePortNumber = null
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Database_maintenance_port_is_not_in_valid_range_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorDatabaseMaintenancePortNumber = "50000"
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);

        }

        [Test]
        [Explicit]
        public void Database_maintenance_port_can_not_be_a_port_in_use_by_the_operating_system_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorDatabaseMaintenancePortNumber = "33333"
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorDatabaseMaintenancePortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Error_database_maintenance_port_is_not_equal_to_port_number_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorDatabaseMaintenancePortNumber = "33333",
                ErrorPortNumber = "33333"
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorDatabaseMaintenancePortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }

        #endregion

        #region errorinstancedestinationpath

        [Test]
        public void Destination_path_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorDestinationPath = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDestinationPath)));
        }

        [Test]
        public void Error_destination_path_cannot_be_null_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorDestinationPath = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorDestinationPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDestinationPath)));
        }

        [Test]
        public void Destination_path_can_be_empty_when_not_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = false,
                SubmitAttempted = true,
                ErrorDestinationPath = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDestinationPath)));

        }

        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void Destination_path_should_not_contain_invalid_characters_when_adding_error_instance(string path)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorDestinationPath = path
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDestinationPath));

            Assert.IsNotEmpty(errors);

        }

        [Test]
        [Explicit]
        public void Destination_path_should_be_unique_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorDestinationPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                ErrorLogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                ErrorDatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs"
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDestinationPath)));
        }
        #endregion

        #region errorinstancelogpath

        [Test]
        public void Error_log_path_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorLogPath = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorLogPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorLogPath)));
        }

        [Test]
        public void Error_log_path_can_be_empty_when_not_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = false,
                SubmitAttempted = true,
                ErrorLogPath = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorLogPath));

            Assert.IsEmpty(errors);
        }

        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void Error_log_path_should_not_contain_invalid_characters_when_adding_error_instance(string path)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorLogPath = path
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorLogPath)));

        }

        #endregion

        #region errorinstancedatabasepath

        [Test]
        public void Error_database_path_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorDatabasePath = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDatabasePath)));
        }

        [Test]
        public void Error_database_path_can_be_empty_when_not_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = false,
                SubmitAttempted = true,
                ErrorDatabasePath = null
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDatabasePath)));
        }

        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void Error_database_path_should_not_contain_invalid_characters_when_adding_error_instance(string path)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorDatabasePath = path
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDatabasePath)));
        }

        [Test]
        [Explicit]
        public void Error_database_path_should_be_unique_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorDestinationPath =
                            "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                ErrorLogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                ErrorDatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs"
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorDatabasePath)));
        }
        #endregion

        #region errorqueuename
        [Test]
        public void Error_queue_name_should_not_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorQueueName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorQueueName)));
        }

        [Test]
        public void Error_queue_name_should_not_be_null_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorQueueName = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorQueueName)));
        }
        #endregion

        #region errorforwardingqueuename
        [Test]
        public void Error_forwarding_queue_name_should_not_be_null_if_error_forwarding_enabled_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorForwarding = new ForwardingOption() { Name = "On", Value = true },
                ErrorForwardingQueueName = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorForwardingQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorForwardingQueueName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Error_forwarding_queue_name_can_not_be_empty_if_error_forwarding_enabled_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorForwarding = new ForwardingOption() { Name = "On", Value = true },
                ErrorForwardingQueueName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorForwardingQueueName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Error_forwarding_queue_name_can_be_empty_if_error_forwarding_not_enabled_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorForwarding = new ForwardingOption() { Name = "Off", Value = false },
                ErrorForwardingQueueName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorForwardingQueueName));

            Assert.IsEmpty(errors);
        }

        [Test]
        public void Error_forwarding_queue_name_can_be_null_if_error_forwarding_not_enabled_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ErrorForwarding = new ForwardingOption() { Name = "Off", Value = false },
                ErrorForwardingQueueName = null
            };

            viewModel.NotifyOfPropertyChange(viewModel.ErrorForwardingQueueName);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorForwardingQueueName));

            Assert.IsEmpty(errors);
        }
        #endregion

        public IDataErrorInfo GetErrorInfo(object vm) => vm as IDataErrorInfo;

        public INotifyDataErrorInfo GetNotifyErrorInfo(object vm) => vm as INotifyDataErrorInfo;
    }
}
