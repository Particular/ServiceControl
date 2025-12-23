namespace ServiceControl.Config.Tests.Validation
{
    using System.ComponentModel;
    using System.Linq;
    using System.ServiceProcess;
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;
    using UI.InstanceAdd;

    public class AddAuditInstanceValidationTests
    {

        #region ValidateConventionName

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

            Assert.Multiple(() =>
            {
                Assert.That(instanceNamesProvided); // Provided because the convention default auto-fills them on instantiation
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ConventionName)), Is.Empty);
            });
        }

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

            Assert.Multiple(() =>
            {
                Assert.That(instanceNamesProvided, Is.True, "Instance names were not provided.");
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ConventionName)), Is.Empty);
            });
        }

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

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.ErrorInstanceName, Is.EqualTo($"Particular.{viewModel.ConventionName}"));
                Assert.That(viewModel.AuditInstanceName, Is.EqualTo($"Particular.{viewModel.ConventionName}.Audit"));
            });
        }

        #endregion

        #region transportname

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

            Assert.That(errors, Is.Not.Empty);

        }

        [TestTheseTransports("AmazonSQS", "AzureServiceBus", "SQLServer", "RabbitMQ", "PostgreSQL")]
        public void Transport_connection_string_cannot_be_empty_if_sample_connection_string_is_present_when_adding_audit_instance(string transportInfoName)
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

            Assert.That(errors, Is.Not.Empty);
        }

        [TestTheseTransports("AmazonSQS", "AzureServiceBus", "SQLServer", "RabbitMQ", "PostgreSQL")]
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

            Assert.That(errors, Is.Not.Empty);

        }

        #endregion

        #region auditinstancename

        [Test]
        public void Audit_instance_name_cannot_be_same_as_an_existing_windows_service_when_adding_error_instance()
        {
            var windowsServices = ServiceController.GetServices();

            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                SubmitAttempted = true,
                AuditInstanceName = windowsServices.First().ServiceName
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditInstanceName));

            Assert.That(errors, Is.Not.Empty);
        }

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

            Assert.That(errors, Is.Not.Empty);

        }

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

            Assert.That(errors, Is.Empty);
        }

        #endregion

        #region useraccountinfo

        [Test]
        public void User_account_info_cannot_be_empty_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel { SubmitAttempted = true, InstallAuditInstance = true };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var selectedAccount = viewModel.AuditServiceAccount;

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditServiceAccount));
            Assert.Multiple(() =>
            {
                //by default the add instance will always have a value of "LocalSystem"(even if you manually set everything to false or empty)

                Assert.That(selectedAccount, Is.EqualTo("LocalSystem"));

                Assert.That(errors, Is.Empty);
            });

        }

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

            Assert.That(errorServiceAccount, Is.Not.Empty);

        }
        #endregion

        #region hostname

        [Test]
        public void Audit_hostname_can_be_empty_when_not_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = false,
                SubmitAttempted = true,
                AuditHostName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditHostName)), Is.Empty);
        }

        [Test]
        public void Audit_hostname_can_be_null_when_not_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = false,
                SubmitAttempted = true,
                AuditHostName = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditHostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditHostName)), Is.Empty);
        }

        [Test]
        public void Audit_hostname_can_not_be_empty_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                AuditHostName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditHostName)), Is.Not.Empty);
        }

        [Test]
        public void Audit_hostname_can_not_be_null_when_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                AuditHostName = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditHostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditHostName)), Is.Not.Empty);
        }

        [TestCase("192.168.1.1")]
        [TestCase("256.0.0.0")]
        [TestCase("::1")]
        [TestCase("2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
        public void Audit_hostname_can_be_an_ip_address_when_adding_audit_instance(string ipAddress)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                AuditHostName = ipAddress
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditHostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditHostName)), Is.Empty);
        }

        [TestCase("hostname with spaces")]
        [TestCase("bad@hostname")]
        [TestCase("bad#hostname")]
        [TestCase("badhostname...")]
        [TestCase("badhostname[/")]
        public void Audit_hostname_cannot_contain_invalid_characters_when_adding_audit_instance(string invalidHostname)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallAuditInstance = true,
                SubmitAttempted = true,
                AuditHostName = invalidHostname
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditHostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);
            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditHostName));

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Not.Empty, "Hostname validation should exist and trigger for invalid hostnames");
                Assert.That(errors.Cast<string>().Any(error => error.Contains("Hostname is not valid")), Is.True,
                    "Hostname validation should display the exact error message 'Hostname is not valid'");
            });
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

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
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

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
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

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
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

            Assert.That(errors, Is.Not.Empty);
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

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
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

            Assert.That(errors, Is.Not.Empty);

        }

        [Test]
        [Explicit]
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

            Assert.That(errors, Is.Not.Empty);
        }

        [Test]
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

            Assert.That(errors, Is.Not.Empty);
        }

        #endregion

        #region auditinstancedestinationpath

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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDestinationPath)), Is.Not.Empty);
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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDestinationPath)), Is.Not.Empty);
        }

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
            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDestinationPath)), Is.Empty);

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

            Assert.That(errors, Is.Not.Empty);

        }

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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDestinationPath)), Is.Not.Empty);
        }

        #endregion

        #region auditinstancelogpath

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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditLogPath)), Is.Not.Empty);
        }

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

            Assert.That(errors, Is.Empty);
        }

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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditLogPath)), Is.Not.Empty);
        }

        #endregion

        #region auditinstancedatabasepath

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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDatabasePath)), Is.Not.Empty);
        }

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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDatabasePath)), Is.Empty);
        }

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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDatabasePath)), Is.Not.Empty);
        }

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
            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditDatabasePath)), Is.Not.Empty);
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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditQueueName)), Is.Not.Empty);
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

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.AuditQueueName)), Is.Not.Empty);
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

            Assert.That(errors, Is.Not.Empty);
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

            Assert.That(errors, Is.Not.Empty);
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

            Assert.That(errors, Is.Empty);
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

            Assert.That(errors, Is.Empty);
        }

        public INotifyDataErrorInfo GetNotifyErrorInfo(object vm) => vm as INotifyDataErrorInfo;
        #endregion
    }
}
