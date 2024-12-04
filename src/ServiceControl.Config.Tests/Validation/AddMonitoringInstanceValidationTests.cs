namespace ServiceControl.Config.Tests.Validation
{
    using System.ComponentModel;
    using System.Linq;
    using System.ServiceProcess;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using UI.InstanceAdd;

    public class AddMonitoringInstanceValidationTests
    {

        #region instancename
        [Test]
        public void Monitoring_instance_name_cannot_be_same_as_an_existing_windows_service_when_adding_error_instance()
        {
            var windowsServices = ServiceController.GetServices();

            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                InstanceName = windowsServices.First().ServiceName
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.InstanceName));

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Monitoring_instance_name_cannot_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                InstanceName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.InstanceName));

            Assert.That(errors, Is.Not.Empty);

        }
        #endregion

        #region useraccountinfo

        [Test]
        public void User_account_info_cannot_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var selectedAccount = viewModel.ServiceAccount;

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceAccount));

            Assert.Multiple(() =>
            {
                Assert.That(selectedAccount, Is.EqualTo("LocalSystem"));
                Assert.That(errors, Is.Empty);
            });
        }

        //if custom user account is selected, then account name  are required fields
        [Test]
        public void Account_name_cannot_be_empty_if_custom_user_account_is_selected_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                UseProvidedAccount = true,
                ServiceAccount = string.Empty,
                Password = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errorServiceAccount = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceAccount));

            Assert.That(errorServiceAccount, Is.Not.Empty);

        }

        #endregion

        #region hostname

        [Test]
        public void Monitoring_hostname_cannot_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                HostName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)), Is.Not.Empty);

        }

        [Test]
        public void Monitoring_hostname_cannot_be_null_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                HostName = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.HostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)), Is.Not.Empty);
        }

        [TestCase("192.168.1.1")]
        [TestCase("256.0.0.0")]
        public void Monitoring_hostname_can_be_an_ip_address_when_adding_monitoring_instance(string ipAddress)
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                HostName = ipAddress
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.HostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)), Is.Empty);
        }

        #endregion

        #region Portnumber
        [Test]
        public void Port_cannot_be_empty_when_adding_monitoring_instance()
        {

            var viewModel = new MonitoringAddViewModel
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
        public void Port_cannot_be_null_when_adding_monitoring_instance()
        {

            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                PortNumber = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.That(errors, Is.Not.Empty);

        }
        [Test]

        public void Port_is_not_in_valid_range_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel
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
        public void Port_can_not_be_a_port_in_use_by_the_operating_system_when_adding_monitoring_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                PortNumber = "33333"
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.That(errors, Is.Not.Empty);
        }
        #endregion

        #region monitoringinstancedestinationpath

        [Test]
        public void Monitoring_destination_path_cannot_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                DestinationPath = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.DestinationPath));

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
        public void Monitoring_destination_path_cannot_be_null_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                DestinationPath = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.DestinationPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.DestinationPath)), Is.Not.Empty);
        }

        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void Monitoring_destination_path_should_not_contain_invalid_characters_when_adding_monitoring_instance(string path)
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                DestinationPath = path
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.DestinationPath));

            Assert.That(errors, Is.Not.Empty);

        }

        [Test]
        [Explicit]
        public void Monitoring_destination_path_should_be_unique_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                DestinationPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Monitoring\\Logs",
                LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Monitoring\\Logs"
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.DestinationPath)), Is.Not.Empty);
        }
        #endregion

        #region monitoringinstancelogpath

        [Test]
        public void Monitoring_log_path_cannot_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel
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
        public void Monitoring_log_path_should_not_contain_invalid_characters_when_adding_monitoring_instance(string path)
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                LogPath = path
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)), Is.Not.Empty);

        }

        [Test]
        [Explicit]
        public void Monitoring_log_path_should_be_unique_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                DestinationPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Monitoring\\Logs",
                LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Monitoring\\Logs"
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)), Is.Not.Empty);
        }
        #endregion

        #region transportname

        [Test]
        public void Transport_cannot_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                SelectedTransport = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.SelectedTransport));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.SelectedTransport));

            Assert.That(errors, Is.Not.Empty);
        }


        [TestTheseTransports("AmazonSQS", "AzureServiceBus", "SQLServer", "RabbitMQ", "PostgreSQL")]
        public void Transport_connection_string_cannot_be_empty_if_sample_connection_string_is_present_when_adding_monitoring_instance(string transportInfoName)
        {

            var viewModel = new MonitoringAddViewModel
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
        public void Transport_connection_string_cannot_be_null_if_sample_connection_string_is_present_when_adding_monitoring_instance(
            string transportInfoName)
        {
            var viewModel = new MonitoringAddViewModel
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
        #endregion

        #region errorqueuename
        [Test]
        public void Error_queue_name_should_not_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                ErrorQueueName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorQueueName)), Is.Not.Empty);
        }

        [Test]
        public void Error_queue_name_should_not_be_null_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel
            {
                SubmitAttempted = true,
                ErrorQueueName = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorQueueName)), Is.Not.Empty);
        }
        #endregion

        public IDataErrorInfo GetErrorInfo(object vm) => vm as IDataErrorInfo;

        public INotifyDataErrorInfo GetNotifyErrorInfo(object vm) => vm as INotifyDataErrorInfo;
    }
}