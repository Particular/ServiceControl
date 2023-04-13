﻿namespace ServiceControl.Config.Tests.Validation
{
    using System.ComponentModel;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using UI.InstanceAdd;
    using UI.InstanceEdit;

    public class EditErrorInstanceValidationTests
    {
        #region transport

        [TestCase(TransportNames.AmazonSQS)]
        [TestCase(TransportNames.AzureServiceBus)]
        [TestCase(TransportNames.SQLServer)]
        [TestCase(TransportNames.RabbitMQClassicDirectRoutingTopology)]
        public void Transport_connection_string_cannot_be_empty_if_sample_connection_string_is_present_when_editing_error_instance(
           string transportInfoName)
        {
            var viewModel = new ServiceControlEditViewModel
            {
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
        public void Transport_connection_string_cannot_be_null_if_sample_connection_string_is_present_when_editing_error_instance(
            string transportInfoName)
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName),
                SubmitAttempted = true,
                ConnectionString = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ConnectionString));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsNotEmpty(errors);
        }

        [TestCase(TransportNames.MSMQ)]
        public void Transport_connection_string_can_be_empty_if_sample_connection_string_is_not_present_when_editing_error_instance(
           string transportInfoName)
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName),
                SubmitAttempted = true,
                ConnectionString = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsEmpty(errors);
        }

        [TestCase(TransportNames.MSMQ)]
        public void Transport_connection_string_can_be_null_if_sample_connection_string_is_not_present_when_editing_error_instance(
            string transportInfoName)
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName),
                SubmitAttempted = true,
                ConnectionString = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ConnectionString));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsEmpty(errors);
        }

        #endregion

        #region hostname


        [TestCase("!")]
        [TestCase("@")]
        [TestCase("#")]
        [TestCase("$")]
        [TestCase("%")]
        [TestCase("&")]
        [TestCase("(")]
        [TestCase(")")]
        [TestCase("[")]
        [TestCase("]")]
        [TestCase("{")]
        [TestCase("}")]
        public void Audit_hostname_can_only_contain_letters_numbers_dash_or_period_when_editing_error_instance(string invalidHostName)
        {
            var viewModel = new ServiceControlEditViewModel()
            {
                SubmitAttempted = true,
                HostName = invalidHostName
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)));
        }

        [Test]
        public void Error_hostname_can_not_be_empty_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                HostName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)));
        }

        [Test]
        public void Error_hostname_can_not_be_null_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                HostName = null
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)));
        }
        #endregion

        #region Portnumber
        [Test]
        public void Port_cannot_be_empty_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                PortNumber = string.Empty
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Port_is_not_in_valid_range_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                PortNumber = "50000"
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        [Explicit]
        public void Port_can_not_be_a_port_in_use_by_the_operating_system_when_editing_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                PortNumber = "33333"
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Error_port_is_not_equal_to_database_port_number_when_editing_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                DatabaseMaintenancePortNumber = "33333",
                PortNumber = "33333"
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);
        }

        #endregion

        #region DatabaseManintenancePortnumber
        [Test]
        public void Database_maintenance_port_cannot_be_empty_when_editing_error_instance()
        {

            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                DatabaseMaintenancePortNumber = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.DatabaseMaintenancePortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Database_maintenance_port_is_not_in_valid_range_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                DatabaseMaintenancePortNumber = "50000"
            };

            viewModel.NotifyOfPropertyChange(null);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        [Explicit]
        public void Database_maintenance_port_can_not_be_a_port_in_use_by_the_operating_system_when_editing_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                DatabaseMaintenancePortNumber = "33333"
            };

            viewModel.NotifyOfPropertyChange(null);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Error_database_maintenance_port_is_not_equal_to_port_number_when_editing_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                DatabaseMaintenancePortNumber = "33333",
                PortNumber = "33333"
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }

        #endregion

        #region errorinstancelogpath

        [Test]
        public void Error_log_path_cannot_be_empty_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                LogPath = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)));
        }

        [Test]
        public void Error_log_path_cannot_be_null_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                LogPath = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.LogPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)));
        }

        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void Error_log_path_should_not_contain_invalid_characters_when_editing_error_instance(string path)
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                LogPath = path
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.LogPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)));
        }

        [Test]
        [Explicit]
        public void Error_log_path_should_be_unique_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                ServiceControl =
                {
                    DestinationPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                    LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                    DatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs"
                }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)));
        }
        #endregion

        #region errorqueuename

        [Test]
        public void Error_queue_name_should_not_be_empty_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                ErrorQueueName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorQueueName)));
        }

        [Test]
        public void Error_queue_name_should_not_be_null_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
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
        public void Error_forwarding_queue_name_should_not_be_null_if_error_forwarding_enabled_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
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
        public void Error_forwarding_queue_name_cannot_be_empty_if_error_forwarding_enabled_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                ErrorForwarding = new ForwardingOption() { Name = "On", Value = true },
                ErrorForwardingQueueName = string.Empty
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorForwardingQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorForwardingQueueName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Error_forwarding_queue_name_can_be_empty_if_error_forwarding_not_enabled_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                ErrorForwarding = new ForwardingOption() { Name = "Off", Value = false },
                ErrorForwardingQueueName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ErrorForwardingQueueName));

            Assert.IsEmpty(errors);
        }

        [Test]
        public void Error_forwarding_queue_name_can_be_null_if_error_forwarding_not_enabled_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
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

        public INotifyDataErrorInfo GetNotifyErrorInfo(object vm) => vm as INotifyDataErrorInfo;
    }
}