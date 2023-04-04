namespace ServiceControl.Config.Tests.Validation
{
    using NUnit.Framework;
    using UI.InstanceAdd;
    using ServiceControlInstaller.Engine.Instances;
    using System.ComponentModel;

    public class AddAuditInstanceValidationTests
    {

        #region ValidateConventionName
        //  Example: convention name cannot be empty when instance name(s) are not provided
        //  Given the convention name field was left empty
        //    and installing an audit instance with empty name
        //        or installing an audit instance with empty name
        //  When the user tries to save the form
        //  Then a convention name validation error should be present

        [Test]
        public void Convention_name_cannot_be_empty_when_instance_names_are_not_provided()
        {
            var viewModel = new ServiceControlAddViewModel();

            var instanceNamesProvided =
                  (viewModel.InstallAuditInstance
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
        //    and installing an audit instance with empty name
        //        or installing an audit instance with empty name
        //  When the user tries to save the form
        //  Then a convention name validation error should be present

        [Test]
        public void Convention_name_can_be_empty_when_instance_names_are_provided()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                //Adding Service Control instance named Foo
                InstallAuditInstance = true,
                ServiceControlAudit =
                {
                    InstanceName = "Foo"
                }
            };

            var instanceNamesProvided =
                    (viewModel.InstallAuditInstance
                     && !string.IsNullOrWhiteSpace(viewModel.ServiceControlAudit.InstanceName))
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
        // Example: when adding an audit instance the transport cannot be empty
        //  Given an audit instance is being created
        //        and the transport not selected
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void Transport_cannot_be_empty_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
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
        public void Transport_connection_string_cannot_be_empty_if_sample_connection_string_is_present_when_adding_audit_instance(
            string transportInfoName)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
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
        public void Transport_connection_string_cannot_be_null_if_sample_connection_string_is_present_when_adding_audit_instance(
            string transportInfoName)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
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

        #region auditinstancename
        // Example: when adding an audit instance the audit instance name cannot be empty
        //  Given an audit instance is being created
        //        and the audit instance name is empty
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void Audit_instance_name_cannot_be_empty_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit = { InstanceName = string.Empty }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.InstanceName));

            Assert.IsNotEmpty(errors);

        }

        // Example: when not adding an audit instance the audit instance name can be empty
        //  Given an audit instance is being created
        //        and the audit instance name is empty
        //  When the user tries to save the form
        //  Then no audit instance name validation errors occur

        [Test]
        public void Audit_instance_name_can_be_empty_when_not_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                SubmitAttempted = true,
                InstallAuditInstance = false,
                ServiceControlAudit = { InstanceName = string.Empty }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.InstanceName));

            Assert.IsEmpty(errors);

        }

        #endregion

        #region useraccountinfo


        // Example: when  adding an audit instance the user account  cannot be empty
        //   Given an audit instance is being created
        //        and the user account is empty or not selected
        //  When the user tries to save the form
        //  Then a validation error occurs
        [Test]
        public void User_account_info_cannot_be_empty_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                SubmitAttempted = true,
                InstallAuditInstance = true
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var selectedAccount = viewModel.ServiceControlAudit.ServiceAccount;

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.ServiceAccount));
            //by default the add instance will always have a value of "LocalSystem"(even if you manually set everything to false or empty)

            ///ServiceControl.Config\UI\InstanceAdd\ServiceControlAddViewModel.cs line 135
            /// \ServiceControl.Config\UI\SharedInstanceEditor\SharedServiceControlEditorViewModel.cs line #73

            Assert.AreEqual("LocalSystem", selectedAccount);

            Assert.IsEmpty(errors);

        }

        //if custom user account is selected, then account name  are required fields
        [Test]
        public void Account_name_cannot_be_empty_if_custom_user_account_is_selected_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                SubmitAttempted = true,
                InstallAuditInstance = true,
                ServiceControlAudit =
                {
                    UseProvidedAccount = true,
                    ServiceAccount = string.Empty,
                    Password = string.Empty
                }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errorServiceAccount = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.ServiceAccount));

            Assert.IsNotEmpty(errorServiceAccount);

        }
        //TODO: valid if acct/pass is valid
        #endregion

        #region hostname
        [Test]
        public void Erorr_hostname_can_be_empty_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit = { HostName = string.Empty }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);
            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.HostName)));
        }

        [Test]
        public void Error_hostname_can_be_null_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit = { HostName = null }
            };

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.HostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.HostName)));
        }
        #endregion

        #region Portnumber
        [Test]
        public void Port_cannot_be_empty_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit = { PortNumber = string.Empty }
            };


            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.PortNumber));

            Assert.IsNotEmpty(errors);
        }
        [Test]
        //validate that port is numeric and within valid range >= 1 and <= 49151
        public void Port_is_not_in_valid_range_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit = { PortNumber = "50000" }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.PortNumber));

            Assert.IsNotEmpty(errors);
        }

        //TODO: figure out how to write this test
        [Test]
        //validate that port is unique
        [Explicit]
        public void Port_can_not_be_a_port_in_use_by_the_operating_system_when_adding_audit_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit = { PortNumber = "33333" }
            };

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.PortNumber));

            Assert.IsNotEmpty(errors);
        }
        [Test]
        //validate that port is not equal to db port number
        public void Audit_port_is_not_equal_to_database_port_number_when_adding_audit_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit =
                {
                    DatabaseMaintenancePortNumber = "33333",
                    PortNumber = "33333"
                }
            };

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.PortNumber));

            Assert.IsNotEmpty(errors);
        }

        #endregion

        #region DatabaseManintenancePortnumber
        [Test]
        public void Database_maintenance_port_cannot_be_empty_when_adding_audit_instance()
        {

            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit =
                {
                    DatabaseMaintenancePortNumber = null
                }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }
        [Test]
        //validate that port is numeric and within valid range >= 1 and <= 49151
        public void Database_maintenance_port_is_not_in_valid_range_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit =
                {
                    DatabaseMaintenancePortNumber = "50000"
                }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);

        }

        //TODO: figure out how to write this test
        [Test]
        [Explicit]
        //validate that port is unique
        public void Database_maintenance_port_can_not_be_a_port_in_use_by_the_operating_system_when_adding_audit_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit =
                {
                    DatabaseMaintenancePortNumber = "33333"
                }
            };

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }
        [Test]
        //validate that port is not equal to db port number
        public void Audit_database_maintenance_port_is_not_equal_to_port_number_when_adding_audit_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit =
                {
                    DatabaseMaintenancePortNumber = "33333",
                    PortNumber = "33333"
                }
            };

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }

        #endregion

        #region auditinstancedestinationpath
        // Example: when  adding an audit instance the destination path cannot be empty
        //   Given an audit instance is being created
        //        and the destination path is empty
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void Destination_path_cannot_be_empty_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit = { DestinationPath = string.Empty }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DestinationPath)));
        }

        [Test]
        public void Audit_destination_path_cannot_be_null_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit = { DestinationPath = null }
            };

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.DestinationPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DestinationPath)));
        }

        // Example: when not adding an audit instance the destination path can be empty
        //   Given an audit instance is being created
        //        and the destination path is empty
        //  When the user tries to save the form
        //  Then no destination path validation errors occur

        [Test]
        public void Destination_path_can_be_empty_when_not_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = false,
                SubmitAttempted = true,
                ServiceControlAudit =
                {
                    DestinationPath = string.Empty
                }
            };

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.DestinationPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);
            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DestinationPath)));

        }

        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void Destination_path_should_not_contain_invalid_characters_when_adding_audit_instance(string path)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit = { DestinationPath = path }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DestinationPath));

            Assert.IsNotEmpty(errors);

        }

        //TODO: Decide if we can do this in a way that makes sense.
        //We would need other instances to be created in order to validate this isn't using the same path as another instance

        //check path is unique
        [Test]
        [Explicit]
        public void Destination_path_should_be_unique_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit =
                    {
                        DestinationPath =
                            "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                        LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                        DatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs"
                    }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DestinationPath)));

            //  throw new Exception("This test isn't correct yet.");
        }
        #endregion

        #region auditinstancelogpath
        // Example: when  adding an audit instance the log path can be empty
        //   Given an audit instance is being created
        //        and the log path is empty
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void Audit_log_path_cannot_be_empty_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit = { LogPath = null }
            };

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.LogPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.LogPath)));
        }

        // Example: when not adding an audit instance the log path can be empty
        //   Given an audit instance is being created
        //        and the log path is empty
        //  When the user tries to save the form
        //  Then no log path validation errors occur

        [Test]
        public void Audit_log_path_can_be_empty_when_not_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = false,
                SubmitAttempted = true,
                ServiceControlAudit = { LogPath = string.Empty }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.LogPath));

            Assert.IsEmpty(errors);
        }

        //check path is valid
        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void Audit_log_path_should_not_contain_invalid_characters_when_adding_audit_instance(string path)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit = { LogPath = path }
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.LogPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.LogPath)));
        }

        //check path is unique
        [Test]
        [Explicit]
        public void Audit_log_path_should_be_unique_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit =
                    {
                        DestinationPath =
                            "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                        LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                        DatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs"
                    }
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.LogPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.LogPath)));
        }
        #endregion

        #region auditinstancedatabasepath
        // Example: when  adding an audit instance the database path cannot be empty
        //   Given an audit instance is being created
        //        and the database path is empty
        //  When the user tries to save the form
        //  Then a validation error occurs

        [Test]
        public void Audit_database_path_cannot_be_empty_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit =
                {
                    DatabasePath = string.Empty
                }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DatabasePath)));
        }

        // Example: when not adding an audit instance the database path can be empty
        //   Given an audit instance is being created
        //        and the database path is empty
        //  When the user tries to save the form
        //  Then no database path validation errors occur

        [Test]
        public void Audit_database_path_can_be_empty_when_not_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = false,
                SubmitAttempted = true,
                ServiceControlAudit = { DatabasePath = null }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DatabasePath)));
        }

        //check path is valid
        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void Audit_database_path_should_not_contain_invalid_characters_when_adding_audit_instance(string path)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit = { DatabasePath = path }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DatabasePath)));
        }

        //TODO: see if this is something that we want to do or change
        //check path is unique
        [Test]
        [Explicit]
        public void Audit_database_path_should_be_unique_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit =
                    {
                        DestinationPath =
                            "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                        LogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                        DatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs"
                    }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.DatabasePath)));
        }
        #endregion

        #region errorqueuename
        [Test]
        public void Audit_queue_name_should_not_be_empty_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit = { AuditQueueName = string.Empty }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.AuditQueueName)));
        }

        [Test]
        public void Audit_queue_name_should_not_be_null_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit = { AuditQueueName = null }
            };

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.AuditQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.AuditQueueName)));
        }
        #endregion

        #region errorforwardingqueuename
        [Test]
        public void Audit_forwarding_queue_name_should_not_be_null_if_audit_forwarding_enabled_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit =
                {
                    AuditForwarding = new ForwardingOption() { Name = "On", Value = true },
                    AuditForwardingQueueName = null
                }
            };

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(nameof(viewModel.ServiceControlAudit.AuditForwardingQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.AuditForwardingQueueName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Audit_forwarding_queue_name_can_not_be_empty_if_audit_forwarding_enabled_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit =
                {
                    AuditForwarding = new ForwardingOption() { Name = "On", Value = true },
                    AuditForwardingQueueName = string.Empty
                }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.AuditForwardingQueueName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Audit_forwarding_queue_name_can_be_empty_if_audit_forwarding_not_enabled_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit =
                {
                    AuditForwarding = new ForwardingOption() { Name = "Off", Value = false },
                    AuditForwardingQueueName = string.Empty
                }
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.AuditForwardingQueueName));

            Assert.IsEmpty(errors);
        }

        [Test]
        public void Audit_forwarding_queue_name_can_be_null_if_audit_forwarding_not_enabled_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                ServiceControlAudit =
                {
                    AuditForwarding = new ForwardingOption() { Name = "Off", Value = false },
                    AuditForwardingQueueName = null
                }
            };

            viewModel.ServiceControlAudit.NotifyOfPropertyChange(viewModel.ServiceControlAudit.AuditForwardingQueueName);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel.ServiceControlAudit);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlAudit.AuditForwardingQueueName));

            Assert.IsEmpty(errors);
        }
        #endregion


        public IDataErrorInfo GetErrorInfo(object vm) => vm as IDataErrorInfo;

        public INotifyDataErrorInfo GetNotifyErrorInfo(object vm) => vm as INotifyDataErrorInfo;
    }
}
