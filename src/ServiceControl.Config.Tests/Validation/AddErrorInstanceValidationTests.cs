namespace ServiceControl.Config.Tests.Validation
{
    using NUnit.Framework;
    using UI.InstanceAdd;
    using ServiceControlInstaller.Engine.Instances;
    using System.ComponentModel;

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
        public void Convention_name_cannot_be_empty_when_instance_names_are_not_provided()
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
        public void Convention_name_can_be_empty_when_instance_names_are_provided()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                //Adding Service Control instance named Foo
                InstallErrorInstance = true,
                ServiceControl =
                {
                    InstanceName = "Foo"
                }
            };

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
        public void When_convention_name_not_empty_instance_names_should_include_convention_name_overwriting_previous_names()
        {
            var viewModel = new ServiceControlAddViewModel();

            viewModel.ServiceControl.InstanceName = "Error";

            viewModel.ServiceControlAudit.InstanceName = "Audit";

            viewModel.ConventionName = "Something";

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
        public void Transport_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                SelectedTransport = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.SelectedTransport));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.SelectedTransport));

            Assert.IsNotEmpty(errors);
        }


        [TestCase(TransportNames.AmazonSQS)]
        [TestCase(TransportNames.AzureServiceBus)]
        [TestCase(TransportNames.SQLServer)]
        [TestCase(TransportNames.RabbitMQClassicDirectRoutingTopology)]
        public void Transport_connection_string_cannot_be_empty_if_sample_connection_string_is_present_when_adding_error_instance(
            string transportInfoName)
        {

            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
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
        public void Transport_connection_string_cannot_be_null_if_sample_connection_string_is_present_when_adding_error_instance(
            string transportInfoName)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName),
                SubmitAttempted = true,
                ConnectionString = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ConnectionString));

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
        public void Error_instance_name_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl = { InstanceName = string.Empty }
            };

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
        public void Error_instance_name_can_be_empty_when_not_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                SubmitAttempted = true,
                InstallErrorInstance = false,
                ServiceControl = { InstanceName = string.Empty }
            };

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
        public void User_account_info_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                SubmitAttempted = true,
                InstallErrorInstance = true
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var selectedAccount = viewModel.ServiceControl.ServiceAccount;

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ServiceAccount));
            //by default the add instance will always have a value of "LocalSystem"(even if you manually set everything to false or empty)

            Assert.AreEqual("LocalSystem", selectedAccount);

            Assert.IsEmpty(errors);
        }

        //if custom user account is selected, then account name  are required fields
        [Test]
        public void Account_name_cannot_be_empty_if_custom_user_account_is_selected_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                SubmitAttempted = true,
                InstallErrorInstance = true,
                ServiceControl =
                    {
                        UseProvidedAccount = true, ServiceAccount = string.Empty,
                        Password = string.Empty
                    }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errorServiceAccount = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ServiceAccount));

            Assert.IsNotEmpty(errorServiceAccount);

        }
        //TODO: valid if acct/pass is valid
        #endregion

        #region hostname
        [Test]
        public void Error_hostname_can_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl = { HostName = string.Empty }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);
            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.HostName)));
        }

        [Test]
        public void Erorr_hostname_can_be_null_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl = { HostName = null }
            };

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.HostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.HostName)));
        }
        #endregion

        #region Portnumber
        [Test]
        public void Port_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl = { PortNumber = null }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.PortNumber));

            Assert.IsNotEmpty(errors);
        }
        [Test]
        //validate that port is numeric and within valid range >= 1 and <= 49151
        public void Port_is_not_in_valid_range_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl = { PortNumber = "50000" }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.PortNumber));

            Assert.IsNotEmpty(errors);
        }

        //TODO: figure out how to write this test
        [Test]
        [Explicit]
        //validate that port is unique
        public void Port_can_not_be_a_port_in_use_by_the_operating_system_when_adding_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl = { PortNumber = "33333" }
            };

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.PortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        //validate that port is not equal to db port number
        public void Error_port_is_not_equal_to_database_port_number_when_adding_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl =
                {
                    DatabaseMaintenancePortNumber = "33333",
                    PortNumber = "33333"
                }
            };

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.PortNumber));

            Assert.IsNotEmpty(errors);
        }

        #endregion

        #region DatabaseManintenancePortnumber
        [Test]
        public void Database_maintenance_port_cannot_be_empty_when_adding_error_instance()
        {

            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl =
                {
                    DatabaseMaintenancePortNumber = null
                }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        //validate that port is numeric and within valid range >= 1 and <= 49151
        public void Database_maintenance_port_is_not_in_valid_range_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl =
                {
                    DatabaseMaintenancePortNumber = "50000"
                }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);

        }

        //TODO: figure out how to write this test
        [Test]
        [Explicit]
        //validate that port is unique
        public void Database_maintenance_port_can_not_be_a_port_in_use_by_the_operating_system_when_adding_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl =
                {
                    DatabaseMaintenancePortNumber = "33333"
                }
            };

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.DatabaseMaintenancePortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        //validate that port is not equal to db port number
        public void Error_database_maintenance_port_is_not_equal_to_port_number_when_adding_error_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl =
                {
                    DatabaseMaintenancePortNumber = "33333",
                    PortNumber = "33333"
                }
            };

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.DatabaseMaintenancePortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabaseMaintenancePortNumber));

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
        public void Destination_path_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl = { DestinationPath = string.Empty }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DestinationPath)));
        }

        [Test]
        public void Error_destination_path_cannot_be_null_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl =
                {
                    DestinationPath = null
                }
            };

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.DestinationPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DestinationPath)));
        }

        // Example: when not adding an error instance the destination path can be empty
        //   Given an error instance is being created
        //        and the destination path is empty
        //  When the user tries to save the form
        //  Then no destination path validation errors occur

        [Test]
        public void Destination_path_can_be_empty_when_not_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = false,
                SubmitAttempted = true,
                ServiceControl =
                {
                    DestinationPath = string.Empty
                }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);
            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DestinationPath)));

        }

        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void Destination_path_should_not_contain_invalid_characters_when_adding_error_instance(string path)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl = { DestinationPath = path }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DestinationPath));

            Assert.IsNotEmpty(errors);

        }

        //TODO: Decide if we can do this in a way that makes sense.
        //We would need other instances to be created in order to validate this isn't using the same path as another instance

        //check path is unique
        [Test]
        [Explicit]
        public void Destination_path_should_be_unique_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl =
                    {
                        DestinationPath =
                            "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                        LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                        DatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs"
                    }
            };

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
        public void Error_log_path_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl = { LogPath = null }
            };

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.LogPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.LogPath)));
        }

        // Example: when not adding an error instance the log path can be empty
        //   Given an error instance is being created
        //        and the log path is empty
        //  When the user tries to save the form
        //  Then no log path validation errors occur

        [Test]
        public void Error_log_path_can_be_empty_when_not_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = false,
                SubmitAttempted = true,
                ServiceControl = { LogPath = string.Empty }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.LogPath));

            Assert.IsEmpty(errors);
        }
        //check path is valid
        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void Error_log_path_should_not_contain_invalid_characters_when_adding_error_instance(string path)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl = { LogPath = path }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.LogPath)));

        }

        //check path is unique
        [Test]
        [Explicit]
        public void Error_log_path_should_be_unique_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl =
                    {
                        DestinationPath =
                            "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                        LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                        DatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs"
                    }
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.LogPath));

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
        public void Error_database_path_cannot_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl = { DatabasePath = string.Empty }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabasePath)));
        }

        // Example: when not adding an error instance the database path can be empty
        //   Given an error instance is being created
        //        and the database path is empty
        //  When the user tries to save the form
        //  Then no database path validation errors occur

        [Test]
        public void Error_database_path_can_be_empty_when_not_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = false,
                SubmitAttempted = true,
                ServiceControl = { DatabasePath = null }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabasePath)));
        }

        //check path is valid
        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void Error_database_path_should_not_contain_invalid_characters_when_adding_error_instance(string path)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl = { DatabasePath = path }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabasePath)));
        }

        //TODO: see if this is something that we want to do or change
        //check path is unique
        [Test]
        [Explicit]
        public void Error_database_path_should_be_unique_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl =
                    {
                        DestinationPath =
                            "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                        LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                        DatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs"
                    }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.DatabasePath)));
        }
        #endregion

        #region errorqueuename
        [Test]
        public void Error_queue_name_should_not_be_empty_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl = { ErrorQueueName = string.Empty }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ErrorQueueName)));
        }

        [Test]
        public void Error_queue_name_should_not_be_null_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl = { ErrorQueueName = null }
            };

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.ErrorQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ErrorQueueName)));
        }
        #endregion

        #region errorforwardingqueuename
        [Test]
        public void Error_forwarding_queue_name_should_not_be_null_if_error_forwarding_enabled_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl =
                    {
                        ErrorForwarding = new ForwardingOption() { Name = "On", Value = true },
                        ErrorForwardingQueueName = null
                    }
            };

            viewModel.ServiceControl.NotifyOfPropertyChange(nameof(viewModel.ServiceControl.ErrorForwardingQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ErrorForwardingQueueName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Error_forwarding_queue_name_can_not_be_empty_if_error_forwarding_enabled_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl =
                    {
                        ErrorForwarding = new ForwardingOption() { Name = "On", Value = true },
                        ErrorForwardingQueueName = string.Empty
                    }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ErrorForwardingQueueName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Error_forwarding_queue_name_can_be_empty_if_error_forwarding_not_enabled_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl =
                {
                    ErrorForwarding = new ForwardingOption() { Name = "Off", Value = false },
                    ErrorForwardingQueueName = string.Empty
                }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ErrorForwardingQueueName));

            Assert.IsEmpty(errors);
        }

        [Test]
        public void Error_forwarding_queue_name_can_be_null_if_error_forwarding_not_enabled_when_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                ServiceControl =
                {
                    ErrorForwarding = new ForwardingOption() { Name = "Off", Value = false },
                    ErrorForwardingQueueName = null
                }
            };

            viewModel.ServiceControl.NotifyOfPropertyChange(viewModel.ServiceControl.ErrorForwardingQueueName);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControl);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControl.ErrorForwardingQueueName));

            Assert.IsEmpty(errors);
        }
        #endregion


        public IDataErrorInfo GetErrorInfo(object vm) => vm as IDataErrorInfo;

        public INotifyDataErrorInfo GetNotifyErrorInfo(object vm) => vm as INotifyDataErrorInfo;
    }
}
