namespace ServiceControl.Config.Tests.Validation
{
    using System.ComponentModel;
    using NUnit.Framework;
    using UI.InstanceAdd;
    using ServiceControlInstaller.Engine.Instances;

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
                (viewModel.InstallErrorInstance
                 && !string.IsNullOrWhiteSpace(viewModel.ErrorInstanceName))
                || (viewModel.InstallAuditInstance
                    && !string.IsNullOrWhiteSpace(viewModel.AuditInstanceName));

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
                AuditInstanceName = "Foo"
            };

            var instanceNamesProvided =
                (viewModel.InstallErrorInstance
                 && !string.IsNullOrWhiteSpace(viewModel.ErrorInstanceName))
                || (viewModel.InstallAuditInstance
                    && !string.IsNullOrWhiteSpace(viewModel.AuditInstanceName));

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
        public void
            When_convention_name_not_empty_instance_names_should_include_convention_name_overwriting_previous_names()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                ErrorInstanceName = "Error",
                AuditInstanceName = "Audit",
                ConventionName = "Something"
            };

            Assert.AreEqual($"Particular.{viewModel.ConventionName}", viewModel.ErrorInstanceName);

            Assert.AreEqual($"Particular.{viewModel.ConventionName}.Audit", viewModel.AuditInstanceName);
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
        public void
            Transport_connection_string_cannot_be_empty_if_sample_connection_string_is_present_when_adding_audit_instance(
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
        public void
            Transport_connection_string_cannot_be_null_if_sample_connection_string_is_present_when_adding_audit_instance(
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
                AuditInstanceName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditInstanceName));

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
                AuditInstanceName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditInstanceName));

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
            var viewModel = new ServiceControlAddViewModel { SubmitAttempted = true, InstallAuditInstance = true };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var selectedAccount = viewModel.AuditServiceAccount;

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditServiceAccount));
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
                AuditUseProvidedAccount = true,
                AuditServiceAccount = string.Empty,
                AuditPassword = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errorServiceAccount = notifyErrorInfo.GetErrors(nameof(viewModel.AuditServiceAccount));

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
                AuditHostName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditHostName)));
        }

        [Test]
        public void Error_hostname_can_be_null_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                AuditHostName = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditHostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditHostName)));
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
                AuditPortNumber = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditPortNumber));

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
                AuditPortNumber = "50000"
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditPortNumber));

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
                AuditPortNumber = "33333"
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditPortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditPortNumber));

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
                ServiceControlAudit = { DatabaseMaintenancePortNumber = "33333", PortNumber = "33333" }
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditPortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditPortNumber));

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
                AuditDatabaseMaintenancePortNumber = null
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditDatabaseMaintenancePortNumber));

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
                AuditDatabaseMaintenancePortNumber = "50000"
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditDatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);

        }

        //TODO: figure out how to write this test
        [Test]
        [Explicit]
        //validate that port is unique
        public void
            Database_maintenance_port_can_not_be_a_port_in_use_by_the_operating_system_when_adding_audit_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                AuditDatabaseMaintenancePortNumber = "33333"
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditDatabaseMaintenancePortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditDatabaseMaintenancePortNumber));

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
                AuditDatabaseMaintenancePortNumber = "33333",
                AuditPortNumber = "33333"
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditDatabaseMaintenancePortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditDatabaseMaintenancePortNumber));

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
                AuditDestinationPath = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDestinationPath)));
        }

        [Test]
        public void Audit_destination_path_cannot_be_null_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                AuditDestinationPath = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditDestinationPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDestinationPath)));
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
                AuditDestinationPath = string.Empty
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditDestinationPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDestinationPath)));

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
                AuditDestinationPath = path
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditDestinationPath));

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
                AuditDestinationPath =
                    "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                AuditLogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                AuditDatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs"
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDestinationPath)));

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
                AuditLogPath = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditLogPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditLogPath)));
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
                AuditLogPath = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditLogPath));

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
                AuditLogPath = path
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditLogPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditLogPath)));
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
                AuditDestinationPath =
                    "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                AuditLogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                AuditDatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs"
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditLogPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditLogPath)));
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
                AuditDatabasePath = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDatabasePath)));
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
                AuditDatabasePath = null
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDatabasePath)));
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
                AuditDatabasePath = path
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDatabasePath)));
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
                AuditDestinationPath =
                    "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                AuditLogPath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs",
                AuditDatabasePath = "C:\\ProgramData\\Particular\\ServiceControl\\Particular.Servicecontrol\\Logs"
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDatabasePath)));
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
                AuditQueueName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditQueueName)));
        }

        [Test]
        public void Audit_queue_name_should_not_be_null_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                AuditQueueName = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditQueueName)));
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

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditForwardingQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditForwardingQueueName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Audit_forwarding_queue_name_can_not_be_empty_if_audit_forwarding_enabled_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                AuditForwarding = new ForwardingOption() { Name = "On", Value = true },
                AuditForwardingQueueName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditForwardingQueueName));

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

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditForwardingQueueName));

            Assert.IsEmpty(errors);
        }

        [Test]
        public void Audit_forwarding_queue_name_can_be_null_if_audit_forwarding_not_enabled_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                AuditForwarding = new ForwardingOption() { Name = "Off", Value = false },
                AuditForwardingQueueName = null

            };

            viewModel.NotifyOfPropertyChange(viewModel.AuditForwardingQueueName);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditForwardingQueueName));

            Assert.IsEmpty(errors);
        }

        public IDataErrorInfo GetErrorInfo(object vm) => vm as IDataErrorInfo;

        public INotifyDataErrorInfo GetNotifyErrorInfo(object vm) => vm as INotifyDataErrorInfo;
        #endregion
    }
}

