using System;

namespace ServiceControl.Config.Tests.Validation
{
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using System.ComponentModel;
    using ServiceControl.Config.UI.InstanceEdit;
    using ServiceControl.Config.UI.InstanceAdd;

    public class EditMonitoringInstanceValidationTests
    {


        #region hostname
        [Test]
        public void monitoring_hostname_cannot_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel();
            viewModel.SubmitAttempted = true;
            viewModel.HostName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)));

        }
        [Test]
        public void monitoring_hostname_cannot_be_null_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel();
            viewModel.SubmitAttempted = true;
            viewModel.HostName = null;

            viewModel.NotifyOfPropertyChange(nameof(viewModel.HostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)));

        }
        #endregion

        #region Portnumber
        [Test]
        public void port_cannot_be_empty_when_editing_monitoring_instance()
        {
            
            var viewModel = new MonitoringEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.PortNumber = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);

        }
        [Test]
        public void port_cannot_be_null_when_editing_monitoring_instance()
        {

            var viewModel = new MonitoringEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.PortNumber = null;

            viewModel.NotifyOfPropertyChange(nameof(viewModel.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);

        }
        [Test]
        //validate that port is numeric and within valid range >= 1 and <= 49151
        public void port_is_not_in_valid_range_when_editing_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.PortNumber = "50000";

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);

        }

        //TODO: figure out how to write this test
        [Test]
        [Explicit]
        //validate that port is unique
        public void port_cannot_be_a_port_in_use_by_the_operating_system_when_editing_monitoring_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new MonitoringEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.PortNumber = "33333";

            viewModel.NotifyOfPropertyChange(nameof(viewModel.PortNumber));
            

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);

          // throw new Exception("This test is not correct yet.");

        }
     

        #endregion

      

      

        #region monitoringinstancelogpath
        // Example: when  editing an monitoring instance the log path cannot be empty
        //   Given an monitoring instance is being created
        //        and the log path is empty
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void monitoring_log_path_cannot_be_empty_when_editing_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.LogPath = null;

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
        public void monitoring_log_path_should_not_contain_invalid_characters_when_editing_monitoring_instance(string path)
        {
            var viewModel = new MonitoringEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.LogPath = path;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)));

        }
        
        //check path is unique
        [Test]
        public void monitoring_log_path_should_be_unique_when_editing_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel();

            viewModel.SubmitAttempted = true;
          
            viewModel.LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);    
            
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)));

          //  throw new Exception("This test isn't correct yet.");

        }
        #endregion

      

        #region monitoringqueuename
        [Test]
        public void monitoring_error_queue_name_should_not_be_empty_when_editing_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ErrorQueueName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorQueueName)));
        }

        [Test]
        public void monitoring_error_queue_name_should_not_be_null_when_editing_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ErrorQueueName = null;

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ErrorQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorQueueName)));
        }
        #endregion

    


        public IDataErrorInfo GetErrorInfo(object vm)
        {
            return vm as IDataErrorInfo;
        }
        public INotifyDataErrorInfo GetNotifyErrorInfo(object vm)
        {

            return vm as INotifyDataErrorInfo;
        }
    }
}
