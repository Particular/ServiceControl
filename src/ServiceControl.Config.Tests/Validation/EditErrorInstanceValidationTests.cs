namespace ServiceControl.Config.Tests.Validation
{
    using System.ComponentModel;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using UI.InstanceAdd;
    using UI.InstanceEdit;

    public class EditErrorInstanceValidationTests
    {
        #region transport

        [TestTheseTransports("AmazonSQS", "AzureServiceBus", "SQLServer", "RabbitMQ", "PostgreSQL")]
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

            Assert.That(errors, Is.Not.Empty);
        }

        [TestTheseTransports("AmazonSQS", "AzureServiceBus", "SQLServer", "RabbitMQ", "PostgreSQL")]
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

            Assert.That(errors, Is.Not.Empty);
        }

        [TestTheseTransports("MSMQ")]
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

            Assert.That(errors, Is.Empty);
        }

        [TestTheseTransports("MSMQ")]
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

            Assert.That(errors, Is.Empty);
        }

        #endregion

        #region hostname

        [Test]
        public void Error_hostname_can_not_be_empty_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                HostName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)), Is.Not.Empty);
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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)), Is.Not.Empty);
        }

        [TestCase("192.168.1.1")]
        [TestCase("256.0.0.0")]
        [TestCase("::1")]
        [TestCase("2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
        public void Error_hostname_can_be_an_ip_address_when_editing_an_error_instance(string ipAddress)
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                HostName = ipAddress
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.HostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)), Is.Empty);
        }

        [TestCase("hostname with spaces")]
        [TestCase("bad@hostname")]
        [TestCase("bad#hostname")]
        [TestCase("badhostname...")]
        [TestCase("badhostname[/")]
        public void Error_hostname_cannot_contain_invalid_characters_when_editing_error_instance(string invalidHostname)
        {
            var viewModel = new ServiceControlEditViewModel
            {
                SubmitAttempted = true,
                HostName = invalidHostname
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.HostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.HostName));

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Not.Empty, "Hostname validation should exist and trigger for invalid hostnames");
                Assert.That(errors.Cast<string>().Any(error => error.Contains("Hostname is not valid")), Is.True,
                    "Hostname validation should display the exact error message 'Hostname is not valid'");
            });
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

            Assert.That(errors, Is.Not.Empty);
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

            Assert.That(errors, Is.Not.Empty);
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

            Assert.That(errors, Is.Not.Empty);
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

            Assert.That(errors, Is.Not.Empty);
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

            Assert.That(errors, Is.Not.Empty);
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

            Assert.That(errors, Is.Not.Empty);
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

            Assert.That(errors, Is.Not.Empty);
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

            Assert.That(errors, Is.Not.Empty);
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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)), Is.Not.Empty);
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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)), Is.Not.Empty);
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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)), Is.Not.Empty);
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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)), Is.Not.Empty);
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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorQueueName)), Is.Not.Empty);
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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorQueueName)), Is.Not.Empty);
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

            Assert.That(errors, Is.Not.Empty);
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

            Assert.That(errors, Is.Not.Empty);
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

            Assert.That(errors, Is.Empty);
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

            Assert.That(errors, Is.Empty);
        }

        #endregion

        public INotifyDataErrorInfo GetNotifyErrorInfo(object vm) => vm as INotifyDataErrorInfo;
    }
}