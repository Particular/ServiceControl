﻿namespace ServiceControl.Config.Tests.Validation
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
        public void Transport_connection_string_cannot_be_empty_if_sample_connection_string_is_present_when_editing_audit_instance(
           string transportInfoName)
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
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
        public void Transport_connection_string_cannot_be_null_if_sample_connection_string_is_present_when_editing_audit_instance(
            string transportInfoName)
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName),
                SubmitAttempted = true,
                ConnectionString = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ConnectionString));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsNotEmpty(errors);

        }

        [TestCase(TransportNames.MSMQ)]
        public void Transport_connection_string_can_be_empty_if_sample_connection_string_is_not_present_when_editing_audit_instance(
           string transportInfoName)
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName),
                SubmitAttempted = true,
                ConnectionString = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsEmpty(errors);
        }

        [TestCase(TransportNames.MSMQ)]
        public void Transport_connection_string_can_be_null_if_sample_connection_string_is_not_present_when_editing_audit_instance(
            string transportInfoName)
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SelectedTransport = ServiceControlCoreTransports.Find(transportInfoName),
                SubmitAttempted = true,
                ConnectionString = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ConnectionString));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.ConnectionString));

            Assert.IsEmpty(errors);

        }

        #endregion

        #region hostname 

        [Test]
        public void Audit_hostname_can_not_be_empty_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                HostName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)));

        }

        [Test]
        public void Audit_hostname_can_not_be_null_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                ServiceControlAudit =
                {
                    HostName = null
                }
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.HostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)));
        }

        [TestCase("192.168.1.1")]
        [TestCase("256.0.0.0")]
        public void Audit_hostname_can_be_an_ip_address_when_editing_an_audit_instance(string ipAddress)
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                HostName = ipAddress
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.HostName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.HostName)));
        }

        #endregion

        #region Portnumber
        [Test]
        public void Port_cannot_be_empty_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                PortNumber = null
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);

        }
        [Test]
        public void Port_is_not_in_valid_range_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                PortNumber = "50000"
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        [Explicit]
        public void Port_can_not_be_a_port_in_use_by_the_operating_system_when_editing_audit_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                ServiceControlAudit =
                {
                    PortNumber = "33333"
                }

            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Audit_port_is_not_equal_to_database_port_number_when_editing_audit_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                DatabaseMaintenancePortNumber = "33333",
                PortNumber = "33333"
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.PortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.PortNumber));

            Assert.IsNotEmpty(errors);
        }

        #endregion

        #region DatabaseManintenancePortnumber
        [Test]
        public void Database_maintenance_port_cannot_be_empty_when_editing_audit_instance()
        {

            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                DatabaseMaintenancePortNumber = null
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);

        }

        [Test]
        public void Database_maintenance_port_is_not_in_valid_range_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                DatabaseMaintenancePortNumber = "50000"
            };

            viewModel.NotifyOfPropertyChange(null);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        [Explicit]
        public void Database_maintenance_port_can_not_be_a_port_in_use_by_the_operating_system_when_editing_audit_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                DatabaseMaintenancePortNumber = "33333"
            };

            viewModel.NotifyOfPropertyChange(null);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Audit_database_maintenance_port_is_not_equal_to_port_number_when_editing_audit_instance()
        {
            //port is unique and should not be used in any other instance (audit or error) and any other windows service
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                PortNumber = "33333",
                DatabaseMaintenancePortNumber = "33333",
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.DatabaseMaintenancePortNumber));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.DatabaseMaintenancePortNumber));

            Assert.IsNotEmpty(errors);
        }

        #endregion

        #region auditinstancelogpath

        [Test]
        public void Audit_log_path_cannot_be_empty_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                LogPath = string.Empty
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.LogPath));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)));
        }

        [TestCase(@"<")]
        [TestCase(@">")]
        [TestCase(@"|")]
        [TestCase(@"?")]
        [TestCase(@"*")]
        public void Audit_log_path_should_not_contain_invalid_characters_when_editing_audit_instance(string path)
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                LogPath = path
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.LogPath)));

        }
        #endregion      

        #region auditqueuename
        [Test]
        public void Audit_queue_name_should_not_be_empty_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                AuditQueueName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.IsNotEmpty(notifyErrorInfo.GetErrors(nameof(viewModel.AuditQueueName)));
        }

        [Test]
        public void Audit_queue_name_can_not_be_null_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
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
        public void Audit_forwarding_queue_name_should_not_be_null_if_audit_forwarding_enabled_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                AuditForwarding = new ForwardingOption() { Name = "On", Value = true },
                AuditForwardingQueueName = null
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.AuditForwardingQueueName));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditForwardingQueueName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Audit_forwarding_queue_name_can_not_be_empty_if_audit_forwarding_enabled_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                AuditForwarding = new ForwardingOption() { Name = "On", Value = true },
                AuditForwardingQueueName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditForwardingQueueName));

            Assert.IsNotEmpty(errors);
        }

        [Test]
        public void Audit_forwarding_queue_name_can_be_empty_if_audit_forwarding_not_enabled_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                AuditForwarding = new ForwardingOption() { Name = "Off", Value = false },
                AuditForwardingQueueName = string.Empty
            };

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditForwardingQueueName));

            Assert.IsEmpty(errors);
        }

        [Test]
        public void Audit_forwarding_queue_name_can_be_null_if_audit_forwarding_not_enabled_when_editing_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel
            {
                SubmitAttempted = true,
                AuditForwarding = new ForwardingOption() { Name = "Off", Value = false },
                AuditForwardingQueueName = null
            };

            viewModel.NotifyOfPropertyChange(viewModel.AuditForwardingQueueName);

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            var errors = notifyErrorInfo.GetErrors(nameof(viewModel.AuditForwardingQueueName));

            Assert.IsEmpty(errors);
        }
        #endregion


        public IDataErrorInfo GetErrorInfo(object vm) => vm as IDataErrorInfo;

        public INotifyDataErrorInfo GetNotifyErrorInfo(object vm) => vm as INotifyDataErrorInfo;
    }
}