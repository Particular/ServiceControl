using System;

namespace ServiceControl.Config.Tests.Validation
{
    using NUnit.Framework;
    using UI.InstanceAdd;
    using ServiceControlInstaller.Engine.Instances;
    using System.ComponentModel;
    using UI.InstanceEdit;

    public class EditAuditInstanceValidationTests
    {

        #region transport
        [TestCase(TransportNames.AmazonSQS)]
        [TestCase(TransportNames.AzureServiceBus)]
        [TestCase(TransportNames.SQLServer)]
        [TestCase(TransportNames.RabbitMQClassicDirectRoutingTopology)]
        public void transport_connection_string_cannot_be_empty_if_sample_connection_string_is_present_when_editing_audit_instance(
           string transportInfoName)
        {
            //TODO: test failing need to revisit
            var viewModel = new ServiceControlAuditEditViewModel();

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
        public void transport_connection_string_cannot_be_null_if_sample_connection_string_is_present_when_editing_audit_instance(
            string transportInfoName)
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName);

            viewModel.SubmitAttempted = true;

            viewModel.ConnectionString = null;

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ConnectionString));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsNotEmpty(errors);

        }

        [TestCase(TransportNames.MSMQ)]
        public void transport_connection_string_can_be_empty_if_sample_connection_string_is_not_present_when_editing_audit_instance(
           string transportInfoName)
        {            
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName);

            viewModel.SubmitAttempted = true;

            viewModel.ConnectionString = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsEmpty(errors);
        }
        [TestCase(TransportNames.MSMQ)]
        public void transport_connection_string_can_be_null_if_sample_connection_string_is_not_present_when_editing_audit_instance(
            string transportInfoName)
        {
            var viewModel = new ServiceControlAuditEditViewModel();

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
        public void audit_hostname_can_be_empty_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.HostName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.HostName)));

        }
        [Test]
        public void audit_hostname_can_be_null_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.HostName = null;

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.HostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.HostName)));

        }
        #endregion

        #region Portnumber
        [Test]
        public void port_cannot_be_empty_when_editing_audit_instance()
        {
            
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.PortNumber = null;


            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.PortNumber));

            Assert.IsNotEmpty(errors);

        }
        [Test]
        //validate that port is numeric and within valid range >= 1 and <= 49151
        public void port_is_not_in_valid_range_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.PortNumber = "50000";

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.PortNumber));

            Assert.IsNotEmpty(errors);

        }

        //TODO: figure out how to write this test
        [Test]
        //validate that port is unique
        public void port_can_not_be_a_port_in_use_by_the_operating_system_when_editing_audit_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.PortNumber = "33333";

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.PortNumber));
            

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.PortNumber));
      
            Assert.IsNotEmpty(errors);
          
          // throw new Exception("This test is not correct yet.");

        }
        [Test]
        //validate that port is not equal to db port number
        public void audit_port_is_not_equal_to_database_port_number_when_editing_audit_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;
            
            viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber = "33333";

            viewModel.ServiceControlAudit.PortNumber = "33333";

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.PortNumber));

            Assert.IsNotEmpty(errors);


        }

        #endregion

        #region DatabaseManintenancePortnumber
        [Test]
        public void database_maintenance_port_cannot_be_empty_when_editing_audit_instance()
        {

            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber = null;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);

        }
        [Test]
        //validate that port is numeric and within valid range >= 1 and <= 49151
        public void database_maintenance_port_is_not_in_valid_range_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber = "50000";

            viewModel.NotifyOfPropertyChange(null);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);

        }

        //TODO: figure out how to write this test
        [Test]
        //validate that port is unique
        public void database_maintenance_port_can_not_be_a_port_in_use_by_the_operating_system_when_editing_audit_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber = "33333";

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(null);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber));
          
            Assert.IsNotEmpty(errors);
           
         //   throw new Exception("This test is not correct yet.");

        }
        [Test]
        //validate that port is not equal to db port number
        public void audit_database_maintenance_port_is_not_equal_to_port_number_when_editing_audit_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber = "33333";

            viewModel.ServiceControlAudit.PortNumber = "33333";

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);


        }

        #endregion
      
        #region auditinstancelogpath
        // Example: when  editing an audit instance the log path cannot be empty
        //   Given an error instance is being created
        //        and the log path is empty
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void audit_log_path_cannot_be_empty_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.LogPath = null;

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.LogPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.LogPath)));

        }

        //check path is valid
        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void audit_log_path_should_not_contain_invalid_characters_when_editing_audit_instance(string path)
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.LogPath = path;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.LogPath)));

        }
        
        //check path is unique
        [Test]
        public void audit_log_path_should_be_unique_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.DestinationPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";
            viewModel.ServiceControlAudit.LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";
            viewModel.ServiceControlAudit.DatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);    
            
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.LogPath)));

        //    throw new Exception("This test isn't correct yet.");

        }
        #endregion      

        #region auditqueuename
        [Test]
        public void audit_queue_name_should_not_be_empty_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.AuditQueueName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.AuditQueueName)));
        }

        [Test]
        public void audit_queue_name_should_not_be_null_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.AuditQueueName = null;

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.AuditQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.AuditQueueName)));
        }
        #endregion

        #region errorforwardingqueuename
        [Test]
        public void audit_forwarding_queue_name_should_not_be_null_if_audit_forwarding_enabled_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.AuditForwarding = new ForwardingOption() { Name = "On", Value = true };

            viewModel.ServiceControlAudit.AuditForwardingQueueName = null;

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.AuditForwardingQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.AuditForwardingQueueName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void audit_forwarding_queue_name_can_not_be_empty_if_audit_forwarding_enabled_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.AuditForwarding = new ForwardingOption() { Name = "On", Value = true };

            viewModel.ServiceControlAudit.AuditForwardingQueueName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.AuditForwardingQueueName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void audit_forwarding_queue_name_can_be_empty_if_audit_forwarding_not_enabled_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.AuditForwarding = new ForwardingOption() { Name = "Off", Value = false };

            viewModel.ServiceControlAudit.AuditForwardingQueueName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.AuditForwardingQueueName));

            Assert.IsEmpty(errors);
        }

        [Test]
        public void audit_forwarding_queue_name_can_be_null_if_audit_forwarding_not_enabled_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ServiceControlAudit.AuditForwarding = new ForwardingOption() { Name = "Off", Value = false };

            viewModel.ServiceControlAudit.AuditForwardingQueueName = null;

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(viewModel.ServiceControlAudit.AuditForwardingQueueName);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.AuditForwardingQueueName));

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
