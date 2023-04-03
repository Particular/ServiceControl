using System;

namespace ServiceControl.Config.Tests.Validation
{
    using NUnit.Framework;
    using UI.InstanceAdd;
    using ServiceControlInstaller.Engine.Instances;
    using System.ComponentModel;
    using ServiceControl.Config.UI.InstanceEdit;

    public class AddMonitoringInstanceValidationTests
    {

        #region instancename
        // Example: when adding a monitoring instance the  instance name cannot be empty

        [Test]
        public void monitoring_instance_name_cannot_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.InstanceName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.InstanceName));

            Assert.IsNotEmpty(errors);

        }
        #endregion

        #region useraccountinfo


        // Example: when  adding an monitoring instance the user account  cannot be empty
        [Test]
        public void user_account_info_cannot_be_empty_when_adding_monitoring_instance()
        {            
            var viewModel = new MonitoringAddViewModel();

            viewModel.SubmitAttempted = true;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var selectedAccount = viewModel.ServiceAccount;
            
            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceAccount));
            //by default the add instance will always have a value of "LocalSystem"(even if you manually set everything to false or empty)
           
            ///ServiceControl.Config\UI\InstanceAdd\MonitoringAddViewModel.cs line 135
            /// \ServiceControl.Config\UI\SharedInstanceEditor\SharedServiceControlEditorViewModel.cs line #73
            Assert.AreEqual("LocalSystem", selectedAccount);
            Assert.IsEmpty(errors);

        }

        //if custom user account is selected, then account name  are required fields
        [Test]
        public void account_name_cannot_be_empty_if_custom_user_account_is_selected_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();
            
            viewModel.SubmitAttempted = true;

            viewModel.UseProvidedAccount = true;
            
            viewModel.ServiceAccount = string.Empty;
            
            viewModel.Password = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var selectedAccount = viewModel.ServiceAccount;

            var errorServiceAccount = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceAccount));
           
            Assert.IsNotEmpty(errorServiceAccount);

        }
        
        #endregion

        #region hostname
        [Test]
        public void monitoring_hostname_cannot_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();
            viewModel.SubmitAttempted = true;
            viewModel.HostName = string.Empty;            

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)));

        }
        [Test]
        public void monitoring_hostname_cannot_be_null_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();
            viewModel.SubmitAttempted = true;
            viewModel.HostName = null;
           
            viewModel.NotifyOfPropertyChange(nameof(viewModel.HostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)));

        }
        #endregion

        #region Portnumber
        [Test]
        public void port_cannot_be_empty_when_adding_monitoring_instance()
        {
            
            var viewModel = new MonitoringAddViewModel();
            
            viewModel.SubmitAttempted = true;

            viewModel.PortNumber = string.Empty;

            viewModel.NotifyOfPropertyChange(nameof(viewModel.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));


            Assert.IsNotEmpty(errors);

        }
        [Test]
        public void port_cannot_be_null_when_adding_monitoring_instance()
        {

            var viewModel = new MonitoringAddViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.PortNumber = null;

            viewModel.NotifyOfPropertyChange(nameof(viewModel.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);

        }
        [Test]
        //validate that port is numeric and within valid range >= 1 and <= 49151
        public void port_is_not_in_valid_range_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();            ;

            viewModel.SubmitAttempted = true;

            viewModel.PortNumber = "50000";

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);

        }

        //TODO: figure out how to write this test
        [Test]
        //validate that port is unique
        public void port_can_not_be_a_port_in_use_by_the_operating_system_when_adding_monitoring_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new MonitoringAddViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.PortNumber = "33333";
            
            viewModel.NotifyOfPropertyChange(nameof(viewModel.PortNumber));
            

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));
          
            Assert.IsNotEmpty(errors);
          //  throw new Exception("This test is not correct yet.");

        }
        
        #endregion


        #region monitoringinstancedestinationpath
        // Example: when  adding an monitoring instance the destination path cannot be empty
        //   Given an monitoring instance is being created
        //        and the destination path is empty
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void monitoring_destination_path_cannot_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.DestinationPath = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.DestinationPath));
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.DestinationPath)));
        }

        [Test]
        public void monitoring_destination_path_cannot_be_null_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.DestinationPath = null;

            viewModel.NotifyOfPropertyChange(nameof(viewModel.DestinationPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.DestinationPath)));
        }

        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void monitoring_destination_path_should_not_contain_invalid_characters_when_adding_monitoring_instance(string path)
        {
            var viewModel = new MonitoringAddViewModel();   

            viewModel.SubmitAttempted = true;

            viewModel.DestinationPath = path;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.DestinationPath));

            Assert.IsNotEmpty(errors);

        }

        //TODO: Decide if we can do this in a way that makes sense.
        //We would need other instances to be created in order to validate this isn't using the same path as another instance

        //check path is unique
        [Test]
        public void monitoring_destination_path_should_be_unique_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.DestinationPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Monitoring\\Logs";
            viewModel.LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Monitoring\\Logs";
           
            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.DestinationPath)));

            //throw new Exception("This test isn't correct yet.");
        }
        #endregion

        #region monitoringinstancelogpath
        // Example: when  adding an monitoring instance the log path cannot be empty
        //   Given an monitoring instance is being created
        //        and the log path is empty
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void monitoring_log_path_cannot_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();      

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
        public void monitoring_log_path_should_not_contain_invalid_characters_when_adding_monitoring_instance(string path)
        {
            var viewModel = new MonitoringAddViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.LogPath = path;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)));

        }
        
        //check path is unique
        [Test]
        public void monitoring_log_path_should_be_unique_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.DestinationPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Monitoring\\Logs";
            viewModel.LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Monitoring\\Logs";

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)));

            //  throw new Exception("This test isn't correct yet.");

        }
        #endregion

        #region transportname
        // Example: when adding an monitoring instance the transport cannot be empty
        //  Given an monitoring instance is being created
        //        and the transport not selected
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void transport_cannot_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.SelectedTransport = null;

            viewModel.NotifyOfPropertyChange(nameof(viewModel.SelectedTransport));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.SelectedTransport));

            Assert.IsNotEmpty(errors);

        }


        [TestCase(TransportNames.AmazonSQS)]
        [TestCase(TransportNames.AzureServiceBus)]
        [TestCase(TransportNames.SQLServer)]
        [TestCase(TransportNames.RabbitMQClassicDirectRoutingTopology)]
        public void transport_connection_string_cannot_be_empty_if_sample_connection_string_is_present_when_adding_monitoring_instance(
            string transportInfoName)
        {

            var viewModel = new MonitoringAddViewModel();

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
        public void transport_connection_string_cannot_be_null_if_sample_connection_string_is_present_when_adding_monitoring_instance(
            string transportInfoName)
        {
            var viewModel = new MonitoringAddViewModel();

            viewModel.SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName);

            viewModel.SubmitAttempted = true;

            viewModel.ConnectionString = null;

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ConnectionString));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsNotEmpty(errors);

        }
        #endregion

        #region errorqueuename
        [Test]
        public void error_queue_name_should_not_be_empty_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.ErrorQueueName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ErrorQueueName)));
        }

        [Test]
        public void error_queue_name_should_not_be_null_when_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();

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
