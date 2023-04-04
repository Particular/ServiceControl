namespace ServiceControl.Config.Tests.Validation
{
    using NUnit.Framework;
    using System.ComponentModel;
    using UI.InstanceEdit;

    public class EditMonitoringInstanceValidationTests
    {


        #region hostname
        [Test]
        public void Monitoring_hostname_cannot_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel
            {
                SubmitAttempted = true,
                HostName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)));

        }
        [Test]
        public void Monitoring_hostname_cannot_be_null_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel
            {
                SubmitAttempted = true,
                HostName = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.HostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)));

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

            Assert.IsNotEmpty(errors);

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

            Assert.IsNotEmpty(errors);

        }
        [Test]
        //validate that port is numeric and within valid range >= 1 and <= 49151
        public void Port_is_not_in_valid_range_when_editing_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel
            {
                SubmitAttempted = true,
                PortNumber = "50000"
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);

        }

        //TODO: figure out how to write this test
        [Test]
        [Explicit]
        //validate that port is unique
        public void Port_cannot_be_a_port_in_use_by_the_operating_system_when_editing_monitoring_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new MonitoringEditViewModel { SubmitAttempted = true, PortNumber = "33333" };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);
        }

        #endregion

        #region monitoringinstancelogpath
        // Example: when  editing an monitoring instance the log path cannot be empty
        //   Given an monitoring instance is being created
        //        and the log path is empty
        //  When the user tries to save the form
        //  Then a validation error occurs

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

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)));
        }

        //check path is valid
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

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)));
        }

        //check path is unique
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

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)));
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
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorQueueName)));
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

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorQueueName)));
        }
        #endregion

        public IDataErrorInfo GetErrorInfo(object vm) => vm as IDataErrorInfo;

        public INotifyDataErrorInfo GetNotifyErrorInfo(object vm) => vm as INotifyDataErrorInfo;
    }
}
