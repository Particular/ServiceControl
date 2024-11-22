namespace ServiceControl.Config.Tests.Validation
{
    using System.ComponentModel;
    using NUnit.Framework;
    using UI.InstanceEdit;

    public class EditMonitoringInstanceValidationTests
    {
        #region hostname

        [Test]
        public void Monitoring_hostname_cannot_be_empty_when_editng_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel
            {
                SubmitAttempted = true,
                HostName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)), Is.Not.Empty);

        }

        [Test]
        public void Monitoring_hostname_cannot_be_null_when_editing_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel
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
        public void Monitoring_hostname_can_be_an_ip_address_when_editing_a_monitoring_instance(string ipAddress)
        {
            var viewModel = new MonitoringEditViewModel
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
        public void Port_cannot_be_empty_when_editing_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel
            {
                SubmitAttempted = true,
                PortNumber = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.That(errors, Is.Not.Empty);

        }

        [Test]
        public void Port_cannot_be_null_when_editing_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel
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
        public void Port_is_not_in_valid_range_when_editing_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel
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
        public void Port_cannot_be_a_port_in_use_by_the_operating_system_when_editing_monitoring_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new MonitoringEditViewModel { SubmitAttempted = true, PortNumber = "33333" };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.That(errors, Is.Not.Empty);
        }

        #endregion

        #region monitoringinstancelogpath

        [Test]
        public void Monitoring_log_path_cannot_be_empty_when_editing_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel
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
        public void Monitoring_log_path_should_not_contain_invalid_characters_when_editing_monitoring_instance(string path)
        {
            var viewModel = new MonitoringEditViewModel
            {
                SubmitAttempted = true,
                LogPath = path
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)), Is.Not.Empty);
        }

        [Test]
        [Explicit]
        public void Monitoring_log_path_should_be_unique_when_editing_monitoring_instance()
        {
            var viewModel =
                new MonitoringEditViewModel
                {
                    SubmitAttempted = true,
                    LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Monitoring\\Logs"
                };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)), Is.Not.Empty);
        }

        #endregion

        #region monitoringqueuename

        [Test]
        public void Monitoring_error_queue_name_should_not_be_empty_when_editing_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel
            {
                SubmitAttempted = true,
                ErrorQueueName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorQueueName)), Is.Not.Empty);
        }

        [Test]
        public void Monitoring_error_queue_name_should_not_be_null_when_editing_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel
            {
                SubmitAttempted = true,
                ErrorQueueName = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorQueueName)), Is.Not.Empty);
        }

        #endregion

        public INotifyDataErrorInfo GetNotifyErrorInfo(object vm) => vm as INotifyDataErrorInfo;
    }
}