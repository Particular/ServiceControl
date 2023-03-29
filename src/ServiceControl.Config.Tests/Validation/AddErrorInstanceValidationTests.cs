namespace ServiceControl.Config.Tests.Validation
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceAdd;
    using ServiceControl.Config.UI.SharedInstanceEditor;
    using ServiceControlInstaller.Engine.Instances;
    using System;
    using System.ComponentModel;
    using System.Diagnostics;

    public class AddErrorInstanceValidationTests
    {

        #region ValidateConventionName
        //  Example: convention name cannot be empty when instance name(s) are not provided
        //  Given the convention name field was left empty
        //    and installing an error instance with empty name
        //        or installing an audit instance with empty name
        //  When the user tries to save the form
        //  Then a convention name validation error should be present

        [Test]
        public void convention_name_cannot_be_empty_when_instance_names_are_not_provided()
        {
            var viewModel = new ServiceControlAddViewModel();

            var instanceNamesProvided =
                  (viewModel.InstallErrorInstance
                   && !string.IsNullOrWhiteSpace(viewModel.ServiceControl.InstanceName))
               || (viewModel.InstallAuditInstance
                   && !string.IsNullOrWhiteSpace(viewModel.ServiceControlAudit.InstanceName));

            viewModel.SubmitAttempted = true;

            //Triggers validation without setting convention name since that would affect the instance names
            viewModel.NotifyOfPropertyChange("ConventionName");

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsFalse(instanceNamesProvided);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ConventionName)));
        }

        //  Example: convention name cannot be empty when instance name(s) are not provided
        //  Given the convention name field was left empty
        //    and installing an error instance with empty name
        //        or installing an audit instance with empty name
        //  When the user tries to save the form
        //  Then a convention name validation error should be present

        [Test]
        public void convention_name_can_be_empty_when_instance_names_are_provided()
        {
            var viewModel = new ServiceControlAddViewModel();

            //Adding Service Control instance named Foo
            viewModel.InstallErrorInstance = true;

            viewModel.ServiceControl.InstanceName = "Foo";

            var instanceNamesProvided =
                    (viewModel.InstallErrorInstance
                     && !string.IsNullOrWhiteSpace(viewModel.ServiceControl.InstanceName))
                 || (viewModel.InstallAuditInstance
                     && !string.IsNullOrWhiteSpace(viewModel.ServiceControlAudit.InstanceName));

            viewModel.SubmitAttempted = true;

            viewModel.ConventionName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsTrue(instanceNamesProvided, "Instance names were not provided.");

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ConventionName)));
        }

        // Example: when convention name provided instance names should include convention name overwriting previous names
        //  Given the convention name is provided
        //  When the user tries to save the form
        //  Then the error instance name should be Particular.<ConventionName>
        //  Then the audit instance name should be Particular.<ConventionName>.Audit

        [Test]
        public void when_convention_name_provided_instance_names_should_include_convention_name_overwritting_previous_names()
        {         
            var viewModel = new ServiceControlAddViewModel();

            viewModel.ServiceControl.InstanceName = "Error";

            viewModel.ServiceControlAudit.InstanceName = "Audit";

            viewModel.ConventionName = "Something";

            var expectedErrorInstanceName = $"Particular.{viewModel.ConventionName}";

            Assert.AreEqual($"Particular.{viewModel.ConventionName}", viewModel.ServiceControl.InstanceName);

            Assert.AreEqual($"Particular.{viewModel.ConventionName}.Audit", viewModel.ServiceControlAudit.InstanceName);
        }


        #endregion

        #region transportname
        // Example: when adding an error instance the transport cannot be empty
        //  Given an error instance is being created
        //        and the transport not selected
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void transport_cannot_be_empty_when_adding_error_instance()
        {
            //TODO - this is failing- Need to revisit
            var viewModel = new ServiceControlAddViewModel();
           
            viewModel.InstallErrorInstance = true;
            viewModel.SelectedTransport = default;
            
            viewModel.SubmitAttempted = true;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel );
            
            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.SelectedTransport));           

            Assert.IsNotEmpty(errors);

        }
       

        [TestCase(TransportNames.AmazonSQS)]
        [TestCase(TransportNames.AzureServiceBus)]
        //[TestCase(TransportNames.MSMQ)]
        [TestCase(TransportNames.SQLServer)]
        [TestCase(TransportNames.RabbitMQClassicDirectRoutingTopology)]
        public void transport_connection_string_cannot_be_empty_if_sample_connection_string_is_present_when_adding_error_instance(string transportInfoName)
        {
            //TODO: test failing need to revisit
            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = true;
            viewModel.SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName); 
           
            viewModel.ConnectionString = string.Empty;
            viewModel.SubmitAttempted = true;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsNotEmpty(errors);

        }
        #endregion

        #region errorinstancename
        // Example: when adding an error instance the error instance name cannot be empty
        //  Given an error instance is being created
        //        and the error instance name is empty
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void error_instance_name_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();
                       
            viewModel.InstallErrorInstance = true;

            viewModel.ServiceControl.InstanceName = string.Empty;

            viewModel.SubmitAttempted = true;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);
            
            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.InstanceName));

            Assert.IsNotEmpty(errors);

        }

        // Example: when not adding an error instance the error instance name can be empty
        //  Given an error instance is being created
        //        and the error instance name is empty
        //  When the user tries to save the form
        //  Then no error instance name validation errors occur

        [Test]
        public void error_instance_name_can_be_empty_when_not_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.SubmitAttempted = true;

            viewModel.InstallErrorInstance = false;
             
            viewModel.ServiceControl.InstanceName = string.Empty;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.InstanceName));

            Assert.IsEmpty(errors);
        }

        #endregion

        #region useraccountinfo
       

        // Example: when  adding an error instance the user account  cannot be empty
        //   Given an error instance is being created
        //        and the user account is empty or not selected
        //  When the user tries to save the form
        //  Then a validation error occurs
        [Test]
        public void user_account_info_cannot_be_empty_when_adding_error_instance()
        {            
            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = true;
           
            viewModel.SubmitAttempted = true;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);
            var selectedAccount = viewModel.ServiceControl.ServiceAccount;
            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ServiceAccount));
            //by default the add instance will always have a value of "LocalSystem"(even if you manually set verything to false or empty)
           
            ///ServiceControl.Config\UI\InstanceAdd\ServiceControlAddViewModel.cs line 135
            /// \ServiceControl.Config\UI\SharedInstanceEditor\SharedServiceControlEditorViewModel.cs line #73
            Assert.AreEqual("LocalSystem", selectedAccount);
            Assert.IsEmpty(errors);

        }

        //if custom user account is selected, then account name  are required fields
        [Test]
        public void accountname_cannot_be_empty_if_custom_user_account_is_selected_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = true;
            viewModel.ServiceControl.UseProvidedAccount = true;
            viewModel.ServiceControl.ServiceAccount = string.Empty;
            viewModel.ServiceControl.Password = string.Empty;

            viewModel.SubmitAttempted = true;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);
            var selectedAccount = viewModel.ServiceControl.ServiceAccount;
            var errorServiceAccount = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ServiceAccount));
           
            Assert.IsNotEmpty(errorServiceAccount);

        }
        //todo valid if acct/pass is valid
        #endregion


        #region Port
        [Test]
        public void port_cannot_be_empty_when_adding_error_instance()
        {
            
            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = true;
            viewModel.ServiceControl.PortNumber = string.Empty;

            viewModel.SubmitAttempted = true;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.PortNumber));

            Assert.IsNotEmpty(errors);

        }
        [Test]
        //validate that port is numeric and within valid range >= 1 and <= 49151
        public void port_is_not_in_valid_range_when_adding_error_instance()
        {

            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = true;
            viewModel.ServiceControl.PortNumber = "500056550";

            viewModel.SubmitAttempted = true;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.PortNumber));

            Assert.IsNotEmpty(errors);

        }
        [Test]
        //validate that port is unique
        public void port_is_unique_when_adding_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = true;
            viewModel.ServiceControl.PortNumber = "33333";

            viewModel.SubmitAttempted = true;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.PortNumber));

            Assert.IsNotEmpty(errors);

        }
        #endregion

        #region errorinstancedestinationpath
        // Example: when  adding an error instance the destination path cannot be empty
        //   Given an error instance is being created
        //        and the destination path is empty
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void error_destination_path_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = true;
            viewModel.ServiceControl.DestinationPath = string.Empty;

            viewModel.SubmitAttempted = true;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DestinationPath)));

        }

        // Example: when not adding an error instance the destination path can be empty
        //   Given an error instance is being created
        //        and the destination path is empty
        //  When the user tries to save the form
        //  Then no destination path validation errors occur

        [Test]
        public void error_destination_path_can_be_empty_when_not_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();           

            viewModel.InstallErrorInstance = false;
            viewModel.ServiceControl.DestinationPath = string.Empty;

            viewModel.SubmitAttempted = true;
            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);
            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DestinationPath)));

        }
        //check path is valid
        [Test]
        public void error_destination_path_should_be_valid_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();           

            viewModel.InstallErrorInstance = true;
            viewModel.ServiceControl.DestinationPath = "/hjd@@?.<>**&&&&";

            viewModel.SubmitAttempted = true;
            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DestinationPath)));

        }
        //check path is unique
        [Test]
        public void error_destination_path_should_be_unique_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = true;
            viewModel.ServiceControl.DestinationPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";
            viewModel.ServiceControl.LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";
            viewModel.ServiceControl.DatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";

            viewModel.SubmitAttempted = true;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DestinationPath)));

        }
        #endregion

        #region errorinstancelogpath
        // Example: when  adding an error instance the log path cannot be empty
        //   Given an error instance is being created
        //        and the log path is empty
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void error_log_path_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();                     

            viewModel.InstallErrorInstance = true;
            viewModel.ServiceControl.LogPath = string.Empty;

            viewModel.SubmitAttempted = true;
            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.LogPath)));

        }

        // Example: when not adding an error instance the log path can be empty
        //   Given an error instance is being created
        //        and the log path is empty
        //  When the user tries to save the form
        //  Then no log path validation errors occur

        [Test]
        public void error_log_path_can_be_empty_when_not_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = false;
            viewModel.ServiceControl.LogPath = string.Empty;

            viewModel.SubmitAttempted = true;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);
            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.LogPath)));

        }
        //check path is valid
        [Test]
        public void error_log_path_should_be_valid_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = true;
            viewModel.ServiceControl.LogPath = "/hjd@@?.<>**&&&&";

            viewModel.SubmitAttempted = true;
            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.LogPath)));

        }
        //check path is unique
        [Test]
        public void error_log_path_should_be_unique_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = true;
            viewModel.ServiceControl.DestinationPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";
            viewModel.ServiceControl.LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";
            viewModel.ServiceControl.DatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";

            viewModel.SubmitAttempted = true;
            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);           
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.LogPath)));

        }
        #endregion

        #region errorinstancedatabasepath
        // Example: when  adding an error instance the database path cannot be empty
        //   Given an error instance is being created
        //        and the database path is empty
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void error_database_path_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = true;
            viewModel.ServiceControl.DatabasePath = string.Empty;

            viewModel.SubmitAttempted = true;
            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabasePath)));

        }

        // Example: when not adding an error instance the database path can be empty
        //   Given an error instance is being created
        //        and the database path is empty
        //  When the user tries to save the form
        //  Then no database path validation errors occur

        [Test]
        public void error_database_path_can_be_empty_when_not_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = false;
            viewModel.ServiceControl.DatabasePath = string.Empty;

            viewModel.SubmitAttempted = true;

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);
            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabasePath)));

        }
        //check path is valid
        [Test]
        public void error_database_path_should_be_valid_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = true;
            viewModel.ServiceControl.DatabasePath = "/hjd@@?.<>**&&&&";

            viewModel.SubmitAttempted = true;
            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabasePath)));

        }
        //check path is unique
        [Test]
        public void error_database_path_should_be_unique_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.InstallErrorInstance = true;
            viewModel.ServiceControl.DestinationPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";
            viewModel.ServiceControl.LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";
            viewModel.ServiceControl.DatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs";

            viewModel.SubmitAttempted = true;
            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabasePath)));

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
