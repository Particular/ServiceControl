using System;

namespace ServiceControl.Config.Tests.Validation
{
    using NUnit.Framework;
    using UI.InstanceAdd;
    using ServiceControlInstaller.Engine.Instances;
    using System.ComponentModel;
    using UI.InstanceEdit;

    public class EditErrorInstanceValidationTests
    {

        #region transport
        [TestCase(TransportNames.AmazonSQS)]
        [TestCase(TransportNames.AzureServiceBus)]
        [TestCase(TransportNames.SQLServer)]
        [TestCase(TransportNames.RabbitMQClassicDirectRoutingTopology)]
        public void transport_connection_string_cannot_be_empty_if_sample_connection_string_is_present_when_editing_error_instance(
           string transportInfoName)
        {
            //TODO: test failing need to revisit
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName);

            viewModel.SubmitAttempted = true;

            viewModel.ConnectionString = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsNotEmpty(errors);
        }

        [TestCase(TransportNames.AmazonSQS)]
        [TestCase(TransportNames.AzureServiceBus)]
        [TestCase(TransportNames.SQLServer)]
        [TestCase(TransportNames.RabbitMQClassicDirectRoutingTopology)]
        public void transport_connection_string_cannot_be_null_if_sample_connection_string_is_present_when_editing_error_instance(
            string transportInfoName)
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName);

            viewModel.SubmitAttempted = true;

            viewModel.ConnectionString = null;

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ConnectionString));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsNotEmpty(errors);

        }

        [TestCase(TransportNames.MSMQ)]
        public void transport_connection_string_can_be_empty_if_sample_connection_string_is_not_present_when_editing_error_instance(
           string transportInfoName)
        {            
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName);

            viewModel.SubmitAttempted = true;

            viewModel.ConnectionString = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsEmpty(errors);
        }
        [TestCase(TransportNames.MSMQ)]
        public void transport_connection_string_can_be_null_if_sample_connection_string_is_not_present_when_editing_error_instance(
            string transportInfoName)
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName);

            viewModel.SubmitAttempted = true;

            viewModel.ConnectionString = null;

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ConnectionString));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsEmpty(errors);

        }

        #endregion

        #region hostname
        [Test]
        public void erorr_hostname_can_be_empty_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.HostName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);
            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.HostName)));

        }
        [Test]
        public void erorr_hostname_can_be_null_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.HostName = null;

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.HostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.HostName)));

        }
        #endregion

        #region Portnumber
        [Test]
        public void port_cannot_be_empty_when_editing_error_instance()
        {
            
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.PortNumber = null;


            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.PortNumber));

            Assert.IsNotEmpty(errors);

        }
        [Test]
        //validate that port is numeric and within valid range >= 1 and <= 49151
        public void port_is_not_in_valid_range_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.PortNumber = "50000";

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.PortNumber));

            Assert.IsNotEmpty(errors);

        }

        //TODO: figure out how to write this test
        [Test]
        //validate that port is unique
        public void port_can_not_be_a_port_in_use_by_the_operating_system_when_editing_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.PortNumber = "33333";
            //viewModel.ServiceControl.DatabaseMaintenancePortNumber = "33334";
            //viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.DatabaseMaintenancePortNumber));
            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.PortNumber));
            

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.PortNumber));
           // var errorsDB = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabaseMaintenancePortNumber));
            Assert.IsNotEmpty(errors);
          //  Assert.IsNotEmpty(errorsDB);
           throw new Exception("This test is not correct yet.");

        }
        [Test]
        //validate that port is not equal to db port number
        public void error_port_is_not_equal_to_database_port_number_when_editing_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;
            
            viewModel.ServiceControl.DatabaseMaintenancePortNumber = "33333";

            viewModel.ServiceControl.PortNumber = "33333";

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.PortNumber));

            Assert.IsNotEmpty(errors);


        }

        #endregion

        #region DatabaseManintenancePortnumber
        [Test]
        public void database_maintenance_port_cannot_be_empty_when_editing_error_instance()
        {

            var viewModel = new ServiceControlEditViewModel();


            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.DatabaseMaintenancePortNumber = null;


            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);

        }
        [Test]
        //validate that port is numeric and within valid range >= 1 and <= 49151
        public void database_maintenance_port_is_not_in_valid_range_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.DatabaseMaintenancePortNumber = "50000";

            viewModel.NotifyOfPropertyChange(null);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);

        }

        //TODO: figure out how to write this test
        [Test]
        //validate that port is unique
        public void database_maintenance_port_can_not_be_a_port_in_use_by_the_operating_system_when_editing_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.DatabaseMaintenancePortNumber = "33333";

            viewModel.ServiceControl.NotifyOfPropertyChange(null);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabaseMaintenancePortNumber));
          
            Assert.IsNotEmpty(errors);
           
            throw new Exception("This test is not correct yet.");

        }
        [Test]
        //validate that port is not equal to db port number
        public void error_database_maintenance_port_is_not_equal_to_port_number_when_editing_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.DatabaseMaintenancePortNumber = "33333";

            viewModel.ServiceControl.PortNumber = "33333";

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.DatabaseMaintenancePortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);


        }

        #endregion

      

        #region errorinstancelogpath
        // Example: when  editing an error instance the log path cannot be empty
        //   Given an error instance is being created
        //        and the log path is empty
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void error_log_path_cannot_be_empty_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.LogPath = null;

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.LogPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.LogPath)));

        }

        //check path is valid
        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void error_log_path_should_not_contain_invalid_characters_when_editing_error_instance(string path)
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.LogPath = path;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.LogPath)));

        }
        
        //check path is unique
        [Test]
        public void error_log_path_should_be_unique_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.DestinationPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";
            viewModel.ServiceControl.LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";
            viewModel.ServiceControl.DatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);    
            
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.LogPath)));

            throw new Exception("This test isn't correct yet.");

        }
        #endregion

      

        #region errorqueuename
        [Test]
        public void error_queue_name_should_not_be_empty_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.ErrorQueueName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ErrorQueueName)));
        }

        [Test]
        public void error_queue_name_should_not_be_null_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.ErrorQueueName = null;

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.ErrorQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ErrorQueueName)));
        }
        #endregion

        #region errorforwardingqueuename
        [Test]
        public void error_forwarding_queue_name_should_not_be_null_if_error_forwarding_enabled_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.ErrorForwarding = new ForwardingOption() { Name = "On", Value = true };

            viewModel.ServiceControl.ErrorForwardingQueueName = null;

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.ErrorForwardingQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ErrorForwardingQueueName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void error_forwarding_queue_name_can_not_be_empty_if_error_forwarding_enabled_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.ErrorForwarding = new ForwardingOption() { Name = "On", Value = true };

            viewModel.ServiceControl.ErrorForwardingQueueName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ErrorForwardingQueueName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void error_forwarding_queue_name_can_be_empty_if_error_forwarding_not_enabled_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.ErrorForwarding = new ForwardingOption() { Name = "Off", Value = false };

            viewModel.ServiceControl.ErrorForwardingQueueName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ErrorForwardingQueueName));

            Assert.IsEmpty(errors);
        }

        [Test]
        public void error_forwarding_queue_name_can_be_null_if_error_forwarding_not_enabled_when_editing_error_instance()
        {
            var viewModel = new ServiceControlEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControl.ErrorForwarding = new ForwardingOption() { Name = "Off", Value = false };

            viewModel.ServiceControl.ErrorForwardingQueueName = null;

            viewModel.ServiceControl.NotifyOfPropertyChange(viewModel.ServiceControl.ErrorForwardingQueueName);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ErrorForwardingQueueName));

            Assert.IsEmpty(errors);
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
